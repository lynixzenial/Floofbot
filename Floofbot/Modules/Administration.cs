﻿using System;
using Discord;
using System.Threading.Tasks;
using Discord.Commands;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;
using Floofbot.Services.Repository;
using Floofbot.Services.Repository.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Floofbot.Modules
{
    public class Administration : ModuleBase<SocketCommandContext>
    {
        private FloofDataContext _floofDB;
        public Administration(FloofDataContext floofDB) => _floofDB = floofDB;

        [Command("ban")]
        [Alias("b")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task YeetUser(string input, [Remainder] string reason = "No Reason Provided")
        {
            IUser badUser = resolveUser(input);
            if (badUser == null) {
                await Context.Channel.SendMessageAsync($"⚠️ Could not resolve user: \"{input}\"");
                return;
            }

            //sends message to user
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "⚖️ Ban Notification";
            builder.Description = $"You have been banned from {Context.Guild.Name}";
            builder.AddField("Reason", reason);
            builder.Color = Color.DarkOrange;
            await badUser.SendMessageAsync("", false, builder.Build());

            //bans the user
            await Context.Guild.AddBanAsync(badUser.Id, 0, $"{Context.User.Username}#{Context.User.Discriminator} -> {reason}");

            builder = new EmbedBuilder();
            builder.Title = (":shield: User Banned");
            builder.Color = Color.DarkOrange;
            builder.Description = $"{badUser.Username}#{badUser.Discriminator} has been banned from {Context.Guild.Name}";
            builder.AddField("User ID", badUser.Id);
            builder.AddField("Moderator", $"{Context.User.Username}#{Context.User.Discriminator}");

            await Context.Channel.SendMessageAsync("", false, builder.Build());
        }
        [Command("kick")]
        [Alias("k")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task kickUser(string user, [Remainder]string reason = "No Reason Provided")
        {
            IUser badUser = resolveUser(user);
            if (badUser == null) {
                await Context.Channel.SendMessageAsync($"⚠️ Could not resolve user: \"{user}\"");
                return;
            }

            //sends message to user
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "🥾 Kick Notification";
            builder.Description = $"You have been Kicked from {Context.Guild.Name}";
            builder.AddField("Reason", reason);
            builder.Color = Color.DarkOrange;
            await badUser.SendMessageAsync("", false, builder.Build());

            //kicks users
            await Context.Guild.GetUser(badUser.Id).KickAsync(reason);
            builder = new EmbedBuilder();
            builder.Title = ("🥾 User Kicked");
            builder.Color = Color.DarkOrange;
            builder.Description = $"{badUser.Username}#{badUser.Discriminator} has been kicked from {Context.Guild.Name}";
            builder.AddField("User ID", badUser.Id);
            builder.AddField("Moderator", $"{Context.User.Username}#{Context.User.Discriminator}");
            await Context.Channel.SendMessageAsync("", false, builder.Build());
        }

        [Command("warn")]
        [Alias("w")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task warnUser(string user, string reason)
        {
            IUser badUser = resolveUser(user);
            if (badUser == null) {
                await Context.Channel.SendMessageAsync($"⚠️ Could not find user \"{user}\"");
                return;
            }

            _floofDB.Add(new Warning {
                DateAdded = DateTime.Now,
                Forgiven = false,
                GuildId = Context.Guild.Id,
                Moderator = Context.User.Id,
                Reason = reason,
                UserId = badUser.Id
            });
            _floofDB.SaveChanges();

            //sends message to user
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "⚖️ Warn Notification";
            builder.Description = $"You have recieved a warning in {Context.Guild.Name}";
            builder.AddField("Reason", reason);
            builder.Color = Color.DarkOrange;
            await badUser.SendMessageAsync("", false, builder.Build());

            builder = new EmbedBuilder();
            builder.Title = (":shield: User Warned");
            builder.Color = Color.DarkOrange;
            builder.AddField("User ID", badUser.Id);
            builder.AddField("Moderator", $"{Context.User.Username}#{Context.User.Discriminator}");

            await Context.Channel.SendMessageAsync("", false, builder.Build());
        }

        [Command("warnlog")]
        [Alias("wl")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task warnlog(string user)
        {
            IUser badUser = resolveUser(user);
            if (badUser == null) {
                await Context.Channel.SendMessageAsync($"⚠️ Could not find user \"{user}\"");
                return;
            }

            var warnings = _floofDB.Warnings.AsQueryable()
                .Where(u => u.UserId == badUser.Id && u.GuildId == Context.Guild.Id)
                .OrderByDescending(x => x.DateAdded);

            EmbedBuilder builder = new EmbedBuilder();
            int warningCount = 0;
            builder.WithTitle($"Warnings for {badUser.Username}#{badUser.Discriminator}");
            foreach (Warning warning in warnings) {
                builder.AddField($"**{warningCount + 1}**. {warning.DateAdded.ToString("yyyy-MM-dd")}", $"```{warning.Reason}```");
                warningCount++;

                //discord embeds have a limit of 25 fields showing the most recent for now. Will look into a paginated reply later
                if(warningCount > 24) 
                    break;
            }
            await Context.Channel.SendMessageAsync("", false, builder.Build());
        }


        [Command("lock")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task ChannelLock()
        {
            try {
                IGuildChannel textChannel = (IGuildChannel)Context.Channel;
                EmbedBuilder builder = new EmbedBuilder {
                    Description = $"🔒  <#{textChannel.Id}> Locked",
                    Color = Color.Orange,

                };
                foreach (IRole role in Context.Guild.Roles.Where(r => !r.Permissions.ManageMessages)) {
                    var perms = textChannel.GetPermissionOverwrite(role).GetValueOrDefault();

                    await textChannel.AddPermissionOverwriteAsync(role, perms.Modify(sendMessages: PermValue.Deny));
                }
                await Context.Channel.SendMessageAsync("", false, builder.Build());
            }
            catch {
                await Context.Channel.SendMessageAsync("Something went wrong!");
            }
        }

        [Command("unlock")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageMessages)]

        public async Task ChannelUnLock()
        {
            try {
                IGuildChannel textChannel = (IGuildChannel)Context.Channel;
                EmbedBuilder builder = new EmbedBuilder {
                    Description = $"🔓  <#{textChannel.Id}> Unlocked",
                    Color = Color.DarkGreen,

                };
                foreach (IRole role in Context.Guild.Roles.Where(r => !r.Permissions.ManageMessages)) {
                    var perms = textChannel.GetPermissionOverwrite(role).GetValueOrDefault();
                    if (role.Name != "nadeko-mute" && role.Name != "Muted")
                        await textChannel.AddPermissionOverwriteAsync(role, perms.Modify(sendMessages: PermValue.Allow));
                }
                await Context.Channel.SendMessageAsync("", false, builder.Build());
            }
            catch {
                await Context.Channel.SendMessageAsync("Something went wrong!");
            }
        }

        // rfurry Discord Rules Gate
        [Command("ireadtherules")]
        public async Task getaccess()
        {
            if (Context.Guild.Id == 225980129799700481) {
                ulong roleID = 494149550622375936;
                var user = (IGuildUser)Context.User;
                await user.AddRoleAsync(Context.Guild.GetRole(roleID));
            }
        }

        private IUser resolveUser(string input)
        {
            IUser user = null;
            //resolve userID or @mention
            if (Regex.IsMatch(input, @"\d{17,18}")) {
                string userID = Regex.Match(input, @"\d{17,18}").Value;
                user = Context.Client.GetUser(Convert.ToUInt64(userID));
            }
            //resolve username#0000
            else if (Regex.IsMatch(input, ".*#[0-9]{4}")) {
                string[] splilt = input.Split("#");
                user = Context.Client.GetUser(splilt[0], splilt[1]);
            }
            return user;
        }
    }
}
