using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace ImmersiveLight.Debugging
{
    internal enum LightDebugRayKind
    {
        Center,
        Face
    }

    internal enum LightDebugLineKind
    {
        All,
        Center,
        Face,
        CenterBlocked,
        FaceBlocked,
        Loose
    }

    internal static class ImmersiveLightDebug
    {
        // light 16 can touch 4991 blocks in the perfect empty diamond (math done at 2 am cut me some slack here 5000 is totally fine debugging now looks like a xmas tree)
        internal const int DefaultMaxLines = 5000;

        private static readonly object LinesLock = new();
        private static readonly List<LightDebugLine> Lines = new();

        private static int version;
        private static bool showCenter = true;
        private static bool showFace = true;
        private static bool showCenterBlocked = true;
        private static bool showFaceBlocked = true;
        private static bool showLoose = true;

        internal static bool RaysEnabled { get; private set; }
        internal static int MaxLines { get; private set; } = DefaultMaxLines;
        internal static float LineWidth { get; private set; } = 3f;
        internal static int Version => version;

        private static readonly int CenterClearColor = ColorUtil.ColorFromRgba(70, 255, 90, 220);
        private static readonly int FaceClearColor = ColorUtil.ColorFromRgba(80, 220, 255, 220);
        private static readonly int CenterBlockedColor = ColorUtil.ColorFromRgba(255, 60, 60, 220);
        private static readonly int FaceBlockedColor = ColorUtil.ColorFromRgba(255, 150, 40, 220);
        private static readonly int LooseSpillColor = ColorUtil.ColorFromRgba(210, 80, 255, 220);

        internal static void SetRays(bool enabled)
        {
            RaysEnabled = enabled;
            if (!enabled)
            {
                Clear(LightDebugLineKind.All);
            }
        }

        internal static void Clear(LightDebugLineKind kind)
        {
            lock (LinesLock)
            {
                if (kind == LightDebugLineKind.All)
                {
                    Lines.Clear();
                }
                else
                {
                    Lines.RemoveAll(line => line.Kind == kind);
                }

                version++;
            }
        }

        internal static void SetMaxLines(int maxLines)
        {
            MaxLines = Math.Max(10, maxLines);
            lock (LinesLock)
            {
                TrimToLimit();
                version++;
            }
        }

        internal static void SetLineWidth(float width)
        {
            LineWidth = GameMath.Clamp(width, 1f, 12f);
        }

        internal static void SetVisible(LightDebugLineKind kind, bool visible)
        {
            lock (LinesLock)
            {
                switch (kind)
                {
                    case LightDebugLineKind.All:
                        showCenter = visible;
                        showFace = visible;
                        showCenterBlocked = visible;
                        showFaceBlocked = visible;
                        showLoose = visible;
                        break;

                    case LightDebugLineKind.Center:
                        showCenter = visible;
                        break;

                    case LightDebugLineKind.Face:
                        showFace = visible;
                        break;

                    case LightDebugLineKind.CenterBlocked:
                        showCenterBlocked = visible;
                        break;

                    case LightDebugLineKind.FaceBlocked:
                        showFaceBlocked = visible;
                        break;

                    case LightDebugLineKind.Loose:
                        showLoose = visible;
                        break;
                }

                version++;
            }
        }

        internal static bool IsVisible(LightDebugLineKind kind)
        {
            lock (LinesLock)
            {
                return IsVisibleNoLock(kind);
            }
        }

        internal static List<LightDebugLine> Snapshot(out int snapshotVersion)
        {
            lock (LinesLock)
            {
                snapshotVersion = version;
                List<LightDebugLine> visibleLines = new(Lines.Count);
                foreach (LightDebugLine line in Lines)
                {
                    if (IsVisibleNoLock(line.Kind))
                    {
                        visibleLines.Add(line);
                    }
                }

                return visibleLines;
            }
        }

        internal static LightDebugStats Stats()
        {
            lock (LinesLock)
            {
                int visible = 0;
                foreach (LightDebugLine line in Lines)
                {
                    if (IsVisibleNoLock(line.Kind))
                    {
                        visible++;
                    }
                }

                return new LightDebugStats(Lines.Count, visible, MaxLines);
            }
        }

        internal static void TraceRay(LightDebugRayKind kind, bool clear, double fromX, double fromY, double fromZ, double toX, double toY, double toZ)
        {
            if (!RaysEnabled)
            {
                return;
            }

            LightDebugLineKind lineKind = RayLineKind(kind, clear);
            AddLine(new Vec3d(fromX, fromY, fromZ), new Vec3d(toX, toY, toZ), lineKind, RayColor(lineKind));
        }

        internal static void TraceLooseSpill(int fromX, int fromY, int fromZ, int toX, int toY, int toZ)
        {
            if (!RaysEnabled)
            {
                return;
            }

            AddLine(new Vec3d(fromX + 0.5, fromY + 0.5, fromZ + 0.5), new Vec3d(toX + 0.5, toY + 0.5, toZ + 0.5), LightDebugLineKind.Loose, LooseSpillColor);
        }

        internal static bool TryParseLineKind(string code, out LightDebugLineKind kind)
        {
            switch (code)
            {
                case "all":
                    kind = LightDebugLineKind.All;
                    return true;

                case "center":
                case "green":
                    kind = LightDebugLineKind.Center;
                    return true;

                case "face":
                case "cyan":
                    kind = LightDebugLineKind.Face;
                    return true;

                case "centerblocked":
                case "red":
                    kind = LightDebugLineKind.CenterBlocked;
                    return true;

                case "faceblocked":
                case "orange":
                    kind = LightDebugLineKind.FaceBlocked;
                    return true;

                case "loose":
                case "purple":
                    kind = LightDebugLineKind.Loose;
                    return true;

                default:
                    kind = LightDebugLineKind.All;
                    return false;
            }
        }

        internal static string LineKindName(LightDebugLineKind kind)
        {
            return kind switch
            {
                LightDebugLineKind.All => "all lines",
                LightDebugLineKind.Center => "green center rays",
                LightDebugLineKind.Face => "cyan face rays",
                LightDebugLineKind.CenterBlocked => "red blocked center rays",
                LightDebugLineKind.FaceBlocked => "orange blocked face rays",
                LightDebugLineKind.Loose => "purple loose spill",
                _ => "unknown lines"
            };
        }


        private static bool IsVisibleNoLock(LightDebugLineKind kind)
        {
            return kind switch
            {
                LightDebugLineKind.All => showCenter && showFace && showCenterBlocked && showFaceBlocked && showLoose,
                LightDebugLineKind.Center => showCenter,
                LightDebugLineKind.Face => showFace,
                LightDebugLineKind.CenterBlocked => showCenterBlocked,
                LightDebugLineKind.FaceBlocked => showFaceBlocked,
                LightDebugLineKind.Loose => showLoose,
                _ => true
            };
        }

        private static void AddLine(Vec3d from, Vec3d to, LightDebugLineKind kind, int color)
        {
            lock (LinesLock)
            {
                Lines.Add(new LightDebugLine(from, to, kind, color));
                TrimToLimit();
                version++;
            }
        }

        private static void TrimToLimit()
        {
            if (Lines.Count > MaxLines)
            {
                Lines.RemoveRange(0, Lines.Count - MaxLines);
            }
        }

        private static LightDebugLineKind RayLineKind(LightDebugRayKind kind, bool clear)
        {
            if (kind == LightDebugRayKind.Center)
            {
                return clear ? LightDebugLineKind.Center : LightDebugLineKind.CenterBlocked;
            }

            return clear ? LightDebugLineKind.Face : LightDebugLineKind.FaceBlocked;
        }

        private static int RayColor(LightDebugLineKind kind)
        {
            return kind switch
            {
                LightDebugLineKind.Center => CenterClearColor,
                LightDebugLineKind.Face => FaceClearColor,
                LightDebugLineKind.CenterBlocked => CenterBlockedColor,
                LightDebugLineKind.FaceBlocked => FaceBlockedColor,
                LightDebugLineKind.Loose => LooseSpillColor,
                _ => CenterClearColor
            };
        }
    }

    internal readonly struct LightDebugLine
    {
        internal readonly Vec3d From;
        internal readonly Vec3d To;
        internal readonly LightDebugLineKind Kind;
        internal readonly int Color;

        internal LightDebugLine(Vec3d from, Vec3d to, LightDebugLineKind kind, int color)
        {
            From = from;
            To = to;
            Kind = kind;
            Color = color;
        }
    }

    internal readonly struct LightDebugStats
    {
        internal readonly int StoredLines;
        internal readonly int VisibleLines;
        internal readonly int MaxLines;

        internal LightDebugStats(int storedLines, int visibleLines, int maxLines)
        {
            StoredLines = storedLines;
            VisibleLines = visibleLines;
            MaxLines = maxLines;
        }
    }
}
