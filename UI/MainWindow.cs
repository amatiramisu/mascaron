using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Mascaron.Core;
using Mascaron.Export;
using Mascaron.GameBridge;
using Mascaron.Visualization;

namespace Mascaron.UI;

public class MainWindow : Window
{
    private enum SculptMode { Move, Rotate, Scale }

    private readonly TopologyGraph topology;
    private readonly BoneTransformState transformState;
    private readonly SculptEngine sculptEngine;
    private readonly MascaronFileFormat fileFormat;
    private readonly CustomizePlusIpc cplusIpc;
    private readonly FileDialogManager fileDialog = new();
    private readonly ITextureProvider textureProvider;
    private readonly string assetsPath;
    private readonly Configuration configuration;

    private string? draggedBone;
    private string? hoveredBone;
    private Vector2 dragStart;
    private List<(string Codename, float Strength)>? dragAffected;

    private string? cachedBrushBone;
    private Vector2 cachedBrushPos;
    private float cachedBrushRadius;
    private List<(string Codename, float Strength)> cachedBrushResult = [];

    private string statusMessage = string.Empty;
    private DateTime statusExpiry;

    private FaceTemplate activeTemplate = FaceTemplate.Standard;
    private FaceTemplate loadedBackgroundTemplate = (FaceTemplate)(-1);
    private ISharedImmediateTexture? backgroundTexture;

    private SculptMode activeMode = SculptMode.Move;
    private bool edgeResizeWasEnabled;
    private float measuredMinWidth;
    private float canvasZoom = 1.0f;
    private Vector2 canvasScroll = Vector2.Zero;
    private float lastViewportSize;
    private Vector2 lastCursorPos;

    private const float HandleRadius = 5.0f;
    private const float HitTestRadius = 8.0f;
    private const float TranslationScale = 0.001f;
    private const float RotationScale = 0.03f;
    private const float ScaleScale = 0.0002f;
    private const float ZoomMin = 0.5f;
    private const float ZoomMax = 3.0f;
    private const float ZoomFactor = 0.12f;

    public void SetTemplate(FaceTemplate template, byte race)
    {
        if (template == activeTemplate)
            return;

        activeTemplate = template;
        transformState.ResetAll();
        loadedBackgroundTemplate = (FaceTemplate)(-1);
        SetStatus($"Race changed — now sculpting {RaceTemplates.GetRaceName(race)}.");
    }

    public MainWindow(
        BoneTransformState transformState,
        SculptEngine sculptEngine,
        MascaronFileFormat fileFormat,
        CustomizePlusIpc cplusIpc,
        Configuration configuration,
        ITextureProvider textureProvider,
        IDalamudPluginInterface pluginInterface)
        : base("Mascaron###MascaronMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 200),
            MaximumSize = new Vector2(1200, 1200),
        };

        this.transformState = transformState;
        this.sculptEngine = sculptEngine;
        this.fileFormat = fileFormat;
        this.cplusIpc = cplusIpc;
        this.configuration = configuration;
        this.textureProvider = textureProvider;
        assetsPath = Path.Combine(pluginInterface.AssemblyLocation.Directory!.FullName, "Assets", "Faces");
        topology = new TopologyGraph(FaceBoneRegistry.Bones);

        if (configuration.WindowWidth.HasValue && configuration.WindowHeight.HasValue)
            Size = new Vector2(configuration.WindowWidth.Value, configuration.WindowHeight.Value);

        if (configuration.WindowX.HasValue && configuration.WindowY.HasValue)
            Position = new Vector2(configuration.WindowX.Value, configuration.WindowY.Value);

        sculptEngine.BrushRadius = configuration.BrushRadius;
        sculptEngine.FalloffCurve = (FalloffCurve)configuration.FalloffCurve;
        sculptEngine.FalloffFactor = configuration.FalloffFactor;
        sculptEngine.TopologyEnabled = configuration.TopologyEnabled;
        sculptEngine.BrushEnabled = configuration.BrushEnabled;
    }

    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.06f, 0.06f, 0.08f, configuration.WindowOpacity));
        edgeResizeWasEnabled = ImGui.GetIO().ConfigWindowsResizeFromEdges;
        ImGui.GetIO().ConfigWindowsResizeFromEdges = false;
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
        ImGui.GetIO().ConfigWindowsResizeFromEdges = edgeResizeWasEnabled;
    }

    public override void OnClose()
    {
        configuration.WindowX = Position?.X;
        configuration.WindowY = Position?.Y;
        var size = Size;
        if (size.HasValue)
        {
            configuration.WindowWidth = size.Value.X;
            configuration.WindowHeight = size.Value.Y;
        }

        configuration.FalloffFactor = sculptEngine.FalloffFactor;
        configuration.MirrorEnabled = sculptEngine.MirrorEnabled;
        configuration.BrushRadius = sculptEngine.BrushRadius;
        configuration.FalloffCurve = (int)sculptEngine.FalloffCurve;
        configuration.TopologyEnabled = sculptEngine.TopologyEnabled;
        configuration.BrushEnabled = sculptEngine.BrushEnabled;
        configuration.Save();
    }

    public override void Draw()
    {
        DrawFileBar();
        var fileBarWidth = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;
        DrawSculptBar();
        var sculptBarWidth = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;
        ImGui.Separator();

        var toolbarWidth = MathF.Max(fileBarWidth, sculptBarWidth) + ImGui.GetStyle().WindowPadding.X;
        if (toolbarWidth != measuredMinWidth)
        {
            measuredMinWidth = toolbarWidth;
            var toolbarHeight = ImGui.GetCursorPosY();
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(measuredMinWidth, measuredMinWidth + toolbarHeight),
                MaximumSize = new Vector2(1200, 1200),
            };
        }

        DrawCanvas();

        fileDialog.Draw();
        DrawHelpBar();
    }

    private void DrawFileBar()
    {
        if (ImGui.Button("Export to C+"))
        {
            if (transformState.ModifiedCount > 0)
            {
                var code = CustomizePlusCodec.Encode(transformState);
                ImGui.SetClipboardText(code);
                SetStatus("Share code copied to clipboard.");
            }
            else
            {
                SetStatus("Nothing to export — no bones modified.");
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Import from C+"))
        {
            var (result, imported) = cplusIpc.ImportActiveProfile();
            switch (result)
            {
                case CustomizePlusIpc.ImportResult.Success:
                    transformState.ResetAll();
                    foreach (var (bone, transform) in imported!.GetModified())
                        transformState.Set(bone, transform);
                    SetStatus($"Imported {imported.ModifiedCount} bone(s) from Customize+.");
                    break;
                case CustomizePlusIpc.ImportResult.NoPlugin:
                    SetStatus("Customize+ is not loaded.");
                    break;
                case CustomizePlusIpc.ImportResult.NoProfile:
                    SetStatus("No active Customize+ profile found.");
                    break;
                case CustomizePlusIpc.ImportResult.NoFaceBones:
                    SetStatus("Customize+ profile has no face bone edits.");
                    break;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Save"))
        {
            if (transformState.ModifiedCount > 0)
            {
                fileDialog.SaveFileDialog("Save Sculpt", ".json", "sculpt.json", ".json", (ok, path) =>
                {
                    if (!ok)
                        return;
                    fileFormat.Save(transformState, path);
                    SetStatus($"Saved to {Path.GetFileName(path)}");
                });
            }
            else
            {
                SetStatus("Nothing to save.");
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Load"))
        {
            fileDialog.OpenFileDialog("Load Sculpt", ".json", (ok, path) =>
            {
                if (!ok)
                    return;
                if (fileFormat.LoadInto(transformState, path))
                    SetStatus($"Loaded {Path.GetFileName(path)}");
                else
                    SetStatus("Failed to load — file malformed.");
            });
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!transformState.CanUndo);
        if (ImGui.Button("Undo"))
            transformState.Undo();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button("Reset All"))
        {
            transformState.ResetAll();
            SetStatus("All bones reset.");
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        var opacity = configuration.WindowOpacity;
        if (ImGui.SliderFloat("Opacity", ref opacity, 0.2f, 1.0f, "%.2f"))
            configuration.WindowOpacity = opacity;

    }

    private void DrawSculptBar()
    {
        DrawModeButton("Move", SculptMode.Move);
        ImGui.SameLine();
        DrawModeButton("Rotate", SculptMode.Rotate);
        ImGui.SameLine();
        DrawModeButton("Scale", SculptMode.Scale);

        ImGui.SameLine();
        ImGui.Text("|");
        ImGui.SameLine();

        DrawToggleButton("Brush", sculptEngine.BrushEnabled, () => sculptEngine.BrushEnabled = !sculptEngine.BrushEnabled);

        ImGui.SameLine();
        DrawCurveButton("Linear", FalloffCurve.Linear);
        ImGui.SameLine();
        DrawCurveButton("Smooth", FalloffCurve.Smooth);
        ImGui.SameLine();
        DrawCurveButton("Sharp", FalloffCurve.Sharp);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        var falloff = sculptEngine.FalloffFactor * 100f;
        if (ImGui.SliderFloat("##Spread", ref falloff, 0f, 100f, "%.0f%%"))
            sculptEngine.FalloffFactor = falloff / 100f;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Spread: 0% = only the dragged bone moves,\n100% = all brush-selected bones move equally.\nThe curve buttons control the gradient shape.");

        ImGui.SameLine();
        ImGui.Text("|");
        ImGui.SameLine();

        var topo = sculptEngine.TopologyEnabled;
        if (ImGui.Checkbox("Region Lock", ref topo))
            sculptEngine.TopologyEnabled = topo;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When enabled, the brush only affects bones\nthat belong to the same facial region as\nthe bone you're dragging (e.g. dragging a\ncheek bone won't pull nearby eye bones).");

        ImGui.SameLine();
        var mirror = sculptEngine.MirrorEnabled;
        if (ImGui.Checkbox("Mirror", ref mirror))
            sculptEngine.MirrorEnabled = mirror;
    }

    private void DrawToggleButton(string label, bool active, Action toggle)
    {
        if (active)
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));

        if (ImGui.Button(label))
            toggle();

        if (active)
            ImGui.PopStyleColor();
    }

    private void DrawCurveButton(string label, FalloffCurve curve)
    {
        if (sculptEngine.FalloffCurve == curve)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            ImGui.Button(label);
            ImGui.PopStyleColor();
        }
        else
        {
            if (ImGui.Button(label))
                sculptEngine.FalloffCurve = curve;
        }
    }

    private void DrawModeButton(string label, SculptMode mode)
    {
        if (activeMode == mode)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            ImGui.Button(label);
            ImGui.PopStyleColor();
        }
        else
        {
            if (ImGui.Button(label))
                activeMode = mode;
        }
    }

    private void DrawCanvas()
    {
        var region = ImGui.GetContentRegionAvail();
        var viewportSize = MathF.Min(region.X, region.Y - GetHelpBarHeight());

        if (lastViewportSize > 0 && lastViewportSize != viewportSize)
            canvasScroll *= viewportSize / lastViewportSize;
        lastViewportSize = viewportSize;
        var canvasOrigin = ImGui.GetCursorScreenPos();

        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(canvasOrigin, canvasOrigin + new Vector2(viewportSize, viewportSize));

        drawList.AddRectFilled(
            canvasOrigin,
            canvasOrigin + new Vector2(viewportSize, viewportSize),
            ImGui.GetColorU32(new Vector4(0.08f, 0.08f, 0.10f, configuration.WindowOpacity)));

        ImGui.InvisibleButton("##canvas", new Vector2(viewportSize, viewportSize));
        HandleCanvasInput(canvasOrigin, viewportSize);

        var logicalSize = viewportSize * canvasZoom;
        var offset = canvasOrigin + canvasScroll;

        DrawFaceBackground(drawList, offset, logicalSize);
        HandleDragInteraction(offset, logicalSize);
        DrawConnectionLines(drawList, offset, logicalSize);
        DrawBoneHandles(drawList, offset, logicalSize);

        if (sculptEngine.BrushEnabled && ImGui.IsItemHovered())
            DrawBrushRing(drawList);

        DrawStatusOverlay(drawList, canvasOrigin);

        drawList.PopClipRect();
    }

    private const int BrushRings = 3;

    private void DrawBrushRing(ImDrawListPtr drawList)
    {
        var mousePos = ImGui.GetIO().MousePos;
        lastCursorPos = mousePos;
        var r = sculptEngine.BrushRadius;
        var segments = Math.Max(24, (int)(r * 0.4f));

        var outerColor = ImGui.GetColorU32(new Vector4(1f, 0.8f, 0.4f, 0.4f));
        drawList.AddCircle(mousePos, r, outerColor, segments, 1.0f);

        for (int i = 1; i <= BrushRings; i++)
        {
            var t = (float)i / (BrushRings + 1);
            var ringRadius = r * (1f - t);
            var strength = sculptEngine.ComputeFalloff(1f - t);
            var alpha = 0.15f + strength * 0.25f;
            var color = ImGui.GetColorU32(new Vector4(1f, 0.8f, 0.4f, alpha));
            drawList.AddCircle(mousePos, ringRadius, color, segments, 1.0f);
        }

        var coreColor = ImGui.GetColorU32(new Vector4(1f, 0.8f, 0.4f, 0.5f));
        drawList.AddCircleFilled(mousePos, 3f, coreColor, 12);
    }

    private void HandleCanvasInput(Vector2 canvasOrigin, float viewportSize)
    {
        var mousePos = ImGui.GetIO().MousePos;

        if (ImGui.IsItemHovered())
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0)
            {
                var ctrlHeld = ImGui.GetIO().KeyCtrl;
                if (sculptEngine.BrushEnabled && !ctrlHeld)
                {
                    sculptEngine.BrushRadius = Math.Clamp(
                        sculptEngine.BrushRadius * MathF.Pow(1f + SculptEngine.BrushRadiusFactor, wheel),
                        SculptEngine.BrushRadiusMin,
                        SculptEngine.BrushRadiusMax);
                }
                else
                {
                    var mouseRelative = mousePos - canvasOrigin;
                    var oldZoom = canvasZoom;
                    canvasZoom = Math.Clamp(canvasZoom * MathF.Pow(1f + ZoomFactor, wheel), ZoomMin, ZoomMax);
                    var zoomDelta = canvasZoom / oldZoom;
                    canvasScroll = mouseRelative - (mouseRelative - canvasScroll) * zoomDelta;
                }
            }

            if (ImGui.IsMouseDragging(ImGuiMouseButton.Right))
            {
                canvasScroll += ImGui.GetIO().MouseDelta;
            }

            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Right))
            {
                canvasZoom = 1.0f;
                canvasScroll = Vector2.Zero;
            }
        }
    }

    private void HandleDragInteraction(Vector2 offset, float logicalSize)
    {
        var mousePos = ImGui.GetIO().MousePos;
        var mouseDown = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var mouseClicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        var mouseReleased = ImGui.IsMouseReleased(ImGuiMouseButton.Left);

        Vector2? ScreenPos(string codename) => GetBoneScreenPosition(codename, offset, logicalSize);

        if (mouseClicked && ImGui.IsItemHovered())
        {
            var hit = HitTestBone(mousePos, offset, logicalSize);
            if (hit != null)
            {
                transformState.PushUndo();
                draggedBone = hit;
                dragStart = mousePos;
                dragAffected = sculptEngine.ComputeAffectedBones(hit, ScreenPos, mousePos);
            }
        }

        if (draggedBone != null && dragAffected != null && mouseDown)
        {
            var delta2D = mousePos - dragStart;
            if (delta2D.LengthSquared() > 0.1f)
            {
                var shiftHeld = ImGui.GetIO().KeyShift;
                switch (activeMode)
                {
                    case SculptMode.Move:
                        var moveDelta = shiftHeld
                            ? new Vector3(0, 0, -delta2D.Y)
                            : new Vector3(-delta2D.X, -delta2D.Y, 0);
                        sculptEngine.ApplyDrag(draggedBone, moveDelta * TranslationScale, dragAffected);
                        break;
                    case SculptMode.Rotate:
                        sculptEngine.ApplyRotation(draggedBone, delta2D * RotationScale, dragAffected, yawAxis: shiftHeld);
                        break;
                    case SculptMode.Scale:
                        sculptEngine.ApplyScale(draggedBone, delta2D * ScaleScale, dragAffected, depthAxis: shiftHeld);
                        break;
                }
                dragStart = mousePos;
            }
        }

        if (mouseReleased)
        {
            draggedBone = null;
            dragAffected = null;
        }

        if (ImGui.IsItemHovered() && draggedBone == null)
            hoveredBone = HitTestBone(mousePos, offset, logicalSize);
        else if (draggedBone == null)
            hoveredBone = null;
    }

    private void DrawFaceBackground(ImDrawListPtr drawList, Vector2 offset, float logicalSize)
    {
        if (loadedBackgroundTemplate != activeTemplate)
        {
            var fileName = RaceTemplates.GetBackgroundFileName(activeTemplate);
            var filePath = Path.Combine(assetsPath, fileName);
            backgroundTexture = File.Exists(filePath) ? textureProvider.GetFromFile(filePath) : null;
            loadedBackgroundTemplate = activeTemplate;
        }

        if (backgroundTexture == null)
            return;

        var wrap = backgroundTexture.GetWrapOrEmpty();
        if (wrap.Handle == nint.Zero)
            return;

        var tint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f));
        drawList.AddImage(wrap.Handle, offset, offset + new Vector2(logicalSize, logicalSize), Vector2.Zero, Vector2.One, tint);
    }

    private Vector2? GetBoneScreenPosition(string codename, Vector2 offset, float logicalSize)
    {
        var pos = RaceTemplates.GetPosition(activeTemplate, codename);
        if (pos == null)
            return null;

        var screenPos = offset + pos.Value * logicalSize;

        var transform = transformState.Get(codename);
        if (transform.Translation != System.Numerics.Vector3.Zero)
        {
            screenPos.X -= transform.Translation.X / TranslationScale;
            screenPos.Y -= transform.Translation.Y / TranslationScale;
        }

        return screenPos;
    }

    private string? HitTestBone(Vector2 mousePos, Vector2 offset, float logicalSize)
    {
        float closestDist = HitTestRadius;
        string? closest = null;

        foreach (var bone in FaceBoneRegistry.Bones)
        {
            var screenPos = GetBoneScreenPosition(bone.Codename, offset, logicalSize);
            if (screenPos == null)
                continue;

            var dist = Vector2.Distance(mousePos, screenPos.Value);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = bone.Codename;
            }
        }

        return closest;
    }

    private void DrawConnectionLines(ImDrawListPtr drawList, Vector2 offset, float logicalSize)
    {
        var activeBone = draggedBone ?? hoveredBone;
        if (activeBone == null)
            return;

        var activePos = GetBoneScreenPosition(activeBone, offset, logicalSize);
        if (activePos == null)
            return;

        if (sculptEngine.MirrorEnabled)
        {
            var mirrorBone = topology.GetMirror(activeBone);
            if (mirrorBone != null)
            {
                var mirrorPos = GetBoneScreenPosition(mirrorBone, offset, logicalSize);
                if (mirrorPos != null)
                {
                    var mirrorColor = ImGui.GetColorU32(new Vector4(0.3f, 0.6f, 0.9f, 0.3f));
                    drawList.AddLine(activePos.Value, mirrorPos.Value, mirrorColor, 1.0f);
                }
            }
        }
    }

    private void DrawBoneHandles(ImDrawListPtr drawList, Vector2 offset, float logicalSize)
    {
        var mousePos = ImGui.GetIO().MousePos;
        var brushBones = new Dictionary<string, float>();
        var activeBone = draggedBone ?? hoveredBone;
        if (activeBone != null && sculptEngine.BrushEnabled)
        {
            var needsRecompute = activeBone != cachedBrushBone
                || Vector2.DistanceSquared(mousePos, cachedBrushPos) > 4f
                || sculptEngine.BrushRadius != cachedBrushRadius;

            if (needsRecompute)
            {
                Vector2? ScreenPos(string codename) => GetBoneScreenPosition(codename, offset, logicalSize);
                cachedBrushResult = sculptEngine.ComputeAffectedBones(activeBone, ScreenPos, mousePos);
                cachedBrushBone = activeBone;
                cachedBrushPos = mousePos;
                cachedBrushRadius = sculptEngine.BrushRadius;
            }

            foreach (var (code, strength) in cachedBrushResult)
                brushBones[code] = strength;
        }

        foreach (var bone in FaceBoneRegistry.Bones)
        {
            var screenPos = GetBoneScreenPosition(bone.Codename, offset, logicalSize);
            if (screenPos == null)
                continue;

            var pos = screenPos.Value;
            var transform = transformState.Get(bone.Codename);

            var depthOffset = Math.Clamp(transform.Translation.Z * 20f, -4f, 4f);

            uint color;
            float radius;

            if (bone.Codename == draggedBone)
            {
                color = ImGui.GetColorU32(new Vector4(1.0f, 0.4f, 0.2f, 1.0f));
                radius = HandleRadius + 2.0f + depthOffset;
            }
            else if (bone.Codename == hoveredBone)
            {
                color = ImGui.GetColorU32(new Vector4(1.0f, 0.85f, 0.4f, 1.0f));
                radius = HandleRadius + 1.0f + depthOffset;
            }
            else if (brushBones.TryGetValue(bone.Codename, out var strength))
            {
                color = ImGui.GetColorU32(new Vector4(1.0f, 0.7f, 0.3f, 0.3f + 0.5f * strength));
                radius = HandleRadius + 1.0f * strength + depthOffset;
            }
            else if (transform.IsModified)
            {
                var brightness = 0.4f + Math.Clamp(depthOffset * 0.05f, -0.15f, 0.15f);
                color = ImGui.GetColorU32(new Vector4(brightness, 0.9f, 0.6f, 0.9f));
                radius = HandleRadius + depthOffset;
            }
            else
            {
                color = ImGui.GetColorU32(new Vector4(0.6f, 0.6f, 0.6f, 0.5f));
                radius = HandleRadius - 1.0f;
            }

            drawList.AddCircleFilled(pos, radius, color);
        }

        if (activeBone != null)
        {
            var bone = FaceBoneRegistry.GetByCodename(activeBone);
            if (bone != null)
            {
                var screenPos = GetBoneScreenPosition(bone.Codename, offset, logicalSize);
                if (screenPos != null)
                {
                    var tooltipPos = screenPos.Value + new Vector2(12, -8);
                    var textColor = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.9f));
                    var dimColor = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.5f));

                    drawList.AddText(tooltipPos, textColor, bone.DisplayName);

                    var transform = transformState.Get(bone.Codename);
                    if (transform.IsModified)
                    {
                        var t = transform.Translation;
                        var r = transform.Rotation;
                        var s = transform.Scaling;
                        var lineY = tooltipPos.Y + 16;

                        if (t != System.Numerics.Vector3.Zero)
                        {
                            drawList.AddText(new Vector2(tooltipPos.X, lineY), dimColor, $"T: {t.X:+0.000;-0.000} {t.Y:+0.000;-0.000} {t.Z:+0.000;-0.000}");
                            lineY += 14;
                        }
                        if (r != System.Numerics.Vector3.Zero)
                        {
                            drawList.AddText(new Vector2(tooltipPos.X, lineY), dimColor, $"R: {r.X:+0.0;-0.0} {r.Y:+0.0;-0.0} {r.Z:+0.0;-0.0}");
                            lineY += 14;
                        }
                        if (s != System.Numerics.Vector3.One)
                        {
                            drawList.AddText(new Vector2(tooltipPos.X, lineY), dimColor, $"S: {s.X:0.000} {s.Y:0.000} {s.Z:0.000}");
                        }
                    }
                }
            }
        }
    }

    private void DrawStatusOverlay(ImDrawListPtr drawList, Vector2 canvasOrigin)
    {
        if (string.IsNullOrEmpty(statusMessage) || DateTime.UtcNow > statusExpiry)
            return;

        var remaining = (float)(statusExpiry - DateTime.UtcNow).TotalSeconds;
        var alpha = Math.Clamp(remaining / 0.5f, 0f, 1f);
        var color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.8f * alpha));
        var pos = canvasOrigin + new Vector2(8, 6);
        drawList.AddText(pos, color, statusMessage);
    }

    private float GetHelpBarHeight()
    {
        return ImGui.GetTextLineHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y + 2f;
    }

    private void DrawHelpBar()
    {
        var contentMax = ImGui.GetWindowContentRegionMax();
        var windowPos = ImGui.GetWindowPos();
        var footerY = contentMax.Y - ImGui.GetTextLineHeightWithSpacing();

        ImGui.SetCursorPosY(footerY);
        ImGui.Separator();

        var dimColor = new Vector4(1f, 1f, 1f, 0.4f);

        if (draggedBone != null)
        {
            var shiftHint = activeMode switch
            {
                SculptMode.Move => ImGui.GetIO().KeyShift
                    ? "Shift+Drag: Z depth  |  Release Shift: X/Y"
                    : "Drag: X/Y  |  Hold Shift: Z depth",
                SculptMode.Rotate => ImGui.GetIO().KeyShift
                    ? "Shift+Drag: Yaw  |  Release Shift: Pitch/Roll"
                    : "Drag: Pitch/Roll  |  Hold Shift: Yaw",
                SculptMode.Scale => ImGui.GetIO().KeyShift
                    ? "Shift+Drag: Z scale  |  Release Shift: X/Y scale"
                    : "Drag: X/Y scale  |  Hold Shift: Z scale",
                _ => string.Empty,
            };
            ImGui.TextColored(dimColor, shiftHint);
        }
        else if (sculptEngine.BrushEnabled)
        {
            ImGui.TextColored(dimColor, "Scroll: Brush size  |  Ctrl+Scroll: Zoom  |  Right-drag: Pan  |  Shift+Drag: Axis toggle");
        }
        else
        {
            ImGui.TextColored(dimColor, "Scroll: Zoom  |  Right-drag: Pan  |  Shift+Drag: Axis toggle  |  Double right-click: Reset view");
        }
    }

    private void SetStatus(string message)
    {
        statusMessage = message;
        statusExpiry = DateTime.UtcNow.AddSeconds(4);
    }
}
