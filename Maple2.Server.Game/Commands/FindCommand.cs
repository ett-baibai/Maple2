﻿using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Text;
using Maple2.Database.Storage;
using Maple2.Model.Enum;
using Maple2.Model.Metadata;
using Maple2.Server.Core.Packets;
using Maple2.Server.Game.Session;

namespace Maple2.Server.Game.Commands;

public class FindCommand : GameCommand {
    public FindCommand(GameSession session,
                       AchievementMetadataStorage achievementStorage,
                       ItemMetadataStorage itemStorage,
                       MapMetadataStorage mapStorage,
                       NpcMetadataStorage npcStorage,
                       QuestMetadataStorage questStorage,
                       SkillMetadataStorage skillStorage) : base(AdminPermissions.Find, "find", "Search database for ids.") {
        AddCommand(new FindSubCommand<AchievementMetadata>(["achievement"], session, achievementStorage));
        AddCommand(new FindSubCommand<ItemMetadata>(["item"], session, itemStorage));
        AddCommand(new FindSubCommand<MapMetadata>(["map"], session, mapStorage));
        AddCommand(new FindSubCommand<NpcMetadata>(["npc", "mob"], session, npcStorage));
        AddCommand(new FindSubCommand<QuestMetadata>(["quest"], session, questStorage));
        AddCommand(new FindSubCommand<StoredSkillMetadata>(["skill"], session, skillStorage));
    }

    private class FindSubCommand<T> : Command where T : ISearchResult {
        private const int PAGE_SIZE = 5;

        private readonly GameSession session;
        private readonly ISearchable<T> storage;

        public FindSubCommand(string[] name, GameSession session, ISearchable<T> storage) : base(name[0], "Search by querying metadata.") {
            if (name.Length > 1) {
                foreach (string alias in name.Skip(1)) {
                    AddAlias(alias);
                }
            }

            this.session = session;
            this.storage = storage;

            var query = new Argument<string[]>("query", "Search query.");
            var page = new Option<int>(["--page", "-p"], "Page of query results.");

            AddArgument(query);
            AddOption(page);
            this.SetHandler<InvocationContext, string[], int>(Handle, query, page);
        }

        private void Handle(InvocationContext ctx, string[] args, int page) {
            try {
                string query = string.Join(' ', args);
                List<T> results = storage.Search(query);
                if (results.Count == 0) {
                    ctx.Console.Out.WriteLine("No results found.");
                    return;
                }

                int pages = (int) Math.Ceiling(results.Count / (float) PAGE_SIZE);
                page = Math.Clamp(page, 1, pages);
                var builder = new StringBuilder($"<b>{results.Count} results for '{query}' ({page}/{pages}):</b>");
                foreach (T result in results.Skip(PAGE_SIZE * (page - 1)).Take(PAGE_SIZE)) {
                    builder.Append($"\n• {result.Id}: {result.Name}");
                }

                session.Send(NoticePacket.Message(builder.ToString(), true));
                ctx.ExitCode = 0;
            } catch (SystemException ex) {
                ctx.Console.Error.WriteLine(ex.Message);
                ctx.ExitCode = 1;
            }
        }
    }
}
