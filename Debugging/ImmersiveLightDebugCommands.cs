using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ImmersiveLight.Debugging
{
    internal static class ImmersiveLightDebugCommands
    {
        private const string StrobeWarning = "WARNING: WILL DRAW A LARGE AMOUNT OF DEBUG LIGHT LINES ALL AT ONCE DURING RELIGHT. MAY CAUSE VISUAL OVERLOAD OR DISCOMFORT IF LOOKING DIRECTLY";

        internal static void Register(ICoreClientAPI api)
        {
            CommandArgumentParsers parsers = api.ChatCommands.Parsers;

            api.ChatCommands.GetOrCreate("ildebug")
                .WithDescription("debug stuff")
                .RequiresPrivilege(Privilege.chat)
                .BeginSubCommand("rays")
                    .WithDescription("draw the rays from the next relights. WARNING: DRAWS A LARGE AMOUNT OF DEBUG LIGHT LINES ALL AT ONCE DURING RELIGHT. MAY CAUSE VISUAL OVERLOAD OR DISCOMFORT IF LOOKING DIRECTLY")
                    .WithArgs(parsers.OptionalBool("on"))
                    .HandleWith(OnDebugRays)
                .EndSubCommand()
                .BeginSubCommand("clear")
                    .WithDescription("clear the current light spaghetti or just one color")
                    .WithArgs(parsers.OptionalWordRange("line", "all", "center", "face", "centerblocked", "faceblocked", "loose", "green", "cyan", "red", "orange", "purple"))
                    .HandleWith(OnDebugClear)
                .EndSubCommand()
                .BeginSubCommand("show")
                    .WithDescription("hide or bring back one color")
                    .WithArgs(parsers.WordRange("line", "all", "center", "face", "centerblocked", "faceblocked", "loose", "green", "cyan", "red", "orange", "purple"), parsers.OptionalBool("on"))
                    .HandleWith(OnDebugShow)
                .EndSubCommand()
                .BeginSubCommand("limit")
                    .WithDescription("cap how many lines get to show")
                    .WithArgs(parsers.OptionalInt("amount"))
                    .HandleWith(OnDebugLimit)
                .EndSubCommand()
                .BeginSubCommand("width")
                    .WithDescription("change line width")
                    .WithArgs(parsers.OptionalFloat("width"))
                    .HandleWith(OnDebugWidth)
                .EndSubCommand()
                .BeginSubCommand("legend")
                    .WithDescription("explain what the colors actually mean because they are not all the same kind of line")
                    .HandleWith(OnDebugLegend)
                .EndSubCommand()
                .BeginSubCommand("stats")
                    .WithDescription("shows stats")
                    .HandleWith(OnDebugStats)
                .EndSubCommand();
        }

        private static TextCommandResult OnDebugRays(TextCommandCallingArgs args)
        {
            bool enabled = args.Parsers[0].IsMissing ? !ImmersiveLightDebug.RaysEnabled : (bool)args[0];
            ImmersiveLightDebug.SetRays(enabled);

            if (enabled)
            {
                return TextCommandResult.Success(StrobeWarning + "\nray debug on now update a light and OH MY GOD TURN AROUND");
            }

            return TextCommandResult.Success("ray debug off");
        }

        private static TextCommandResult OnDebugClear(TextCommandCallingArgs args)
        {
            LightDebugLineKind kind = LightDebugLineKind.All;
            if (!args.Parsers[0].IsMissing)
            {
                ImmersiveLightDebug.TryParseLineKind((string)args[0], out kind);
            }

            ImmersiveLightDebug.Clear(kind);
            return TextCommandResult.Success(ImmersiveLightDebug.LineKindName(kind) + " cleared");
        }

        private static TextCommandResult OnDebugShow(TextCommandCallingArgs args)
        {
            ImmersiveLightDebug.TryParseLineKind((string)args[0], out LightDebugLineKind kind);
            bool visible = args.Parsers[1].IsMissing ? !ImmersiveLightDebug.IsVisible(kind) : (bool)args[1];
            ImmersiveLightDebug.SetVisible(kind, visible);
            return TextCommandResult.Success(ImmersiveLightDebug.LineKindName(kind) + (visible ? " visible again" : " hidden"));
        }

        private static TextCommandResult OnDebugLimit(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success("line limit is " + ImmersiveLightDebug.MaxLines);
            }

            ImmersiveLightDebug.SetMaxLines((int)args[0]);
            return TextCommandResult.Success("line limit is now " + ImmersiveLightDebug.MaxLines);
        }

        private static TextCommandResult OnDebugWidth(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success("line width is " + ImmersiveLightDebug.LineWidth);
            }

            ImmersiveLightDebug.SetLineWidth((float)args[0]);
            return TextCommandResult.Success("line width is now " + ImmersiveLightDebug.LineWidth);
        }

        private static TextCommandResult OnDebugLegend(TextCommandCallingArgs args)
        {
            return TextCommandResult.Success(StrobeWarning + "\n"
                + "source   the actual block light source vanilla is collecting from\n"
                + "target   the block the floodfill is trying to light right now\n"
                + "center   the middle of that target block this is the first and cleanest check\n"
                + "face     a backup point inside the target face looking back at the source not collision not selection just a safer light sample\n"
                + "blocker  usually absorption above 32 but doors and trapdoors can also block when the ray hits their moved collision box\n"
                + "loose    one tiny handoff from a lit visible block to a hidden neighbour so doors/corners do not look carved\n"
                + "green    source to target center worked this is the normal happy path\n"
                + "red      source to target center failed and the line stops on the blocker that killed it\n"
                + "cyan     center failed but a face sample worked still direct light from the source just less blocky\n"
                + "orange   center failed then face backup also failed\n"
                + "purple   loose spill from floodfill block to floodfill block this is the only color that connects neighbours instead of source to target\n"
                + "green cyan red orange are ray checks purple is the handoff do not confuse purple with a ray from the lamp");
        }

        private static TextCommandResult OnDebugStats(TextCommandCallingArgs args)
        {
            LightDebugStats stats = ImmersiveLightDebug.Stats();
            return TextCommandResult.Success("stored " + stats.StoredLines + " lines visible " + stats.VisibleLines + " limit " + stats.MaxLines);
        }
    }
}
