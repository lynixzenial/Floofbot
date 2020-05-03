﻿using Discord;
using Discord.Addons.Interactive;
using Discord.WebSocket;
using Floofbot.Services.Repository;
using Floofbot.Services.Repository.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Floofbot.Services
{
    public class NicknameAlertService : InteractiveBase
    {
        private FloofDataContext _floofDb;
        
        private Dictionary<ulong, SocketGuildUser> messageDic = new Dictionary<ulong, SocketGuildUser>(); 
        private ITextChannel _channel;
        private Emoji banEmoji = new Emoji("🔨");
        private Emoji warnEmoji = new Emoji("⚠️");
        private Emoji kickEmoji = new Emoji("👢");
        private Emoji removeEmoji = new Emoji("📝");

        public NicknameAlertService(FloofDataContext floofDb)
        {
            _floofDb = floofDb;
        }
        public async Task<ITextChannel> GetChannel(Discord.IGuild guild, ulong channelId)
        {
            return await guild.GetTextChannelAsync(channelId);
        }

        public async Task HandleBadNickname(SocketGuildUser badUser, IGuild guild)
        {
            var serverConfig = _floofDb.NicknameAlertConfigs.Find(guild.Id);

            if (serverConfig == null || !serverConfig.IsOn || serverConfig.Channel == 0) // not configured/disabled
            {
                return;
            }
            _channel = await GetChannel(guild, serverConfig.Channel);

            var embed = new EmbedBuilder()
                .WithDescription($"{removeEmoji.Name}: Remove Nickname\n" +
                $"{warnEmoji.Name}: Warn\n" +
                $"{kickEmoji.Name}: Kick\n" +
                $"{banEmoji.Name}: Ban")
                .Build();

            var message = await _channel.SendMessageAsync($"{badUser.Mention} ({badUser.Username}#{badUser.Discriminator}) has been " +
                $"detected with a bad name! What should I do?\n\nNickname: {badUser.Nickname}", false, embed);
            await message.AddReactionAsync(removeEmoji);
            await message.AddReactionAsync(kickEmoji);
            await message.AddReactionAsync(warnEmoji);
            await message.AddReactionAsync(banEmoji);

            messageDic.Add(message.Id, badUser);

        }

        public async Task ReactionCallback(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            var msg = message.Value as IUserMessage;

            if (messageDic.ContainsKey(msg.Id))
            {
                SocketGuildUser badUser;
                messageDic.TryGetValue(msg.Id, out badUser);
                var moderator = badUser.Guild.GetUser(reaction.UserId);

                if (reaction.Emote.Name.Equals(banEmoji.Name))
                {
                    try
                    {
                        //sends message to user
                        EmbedBuilder builder = new EmbedBuilder();
                        builder.Title = "⚖️ Ban Notification";
                        builder.Description = $"You have been banned from {badUser.Guild.Name}";
                        builder.AddField("Reason", "Banned by BOT for an inappropriate name");
                        builder.Color = Color.DarkOrange;
                        await badUser.SendMessageAsync("", false, builder.Build());

                        await badUser.Guild.AddBanAsync(badUser, 0, $"{moderator.Username}#{moderator.Discriminator} ({moderator.Id}) -> Inappropriate Name");

                        await channel.SendMessageAsync($"Got it! I banned {badUser.Username}#{badUser.Discriminator}!");
                    }
                    catch (Exception ex)
                    {
                        await channel.SendMessageAsync("Unable to ban user. Do I have the permissions?");
                        Log.Error("Unable to ban user for bad name: " + ex);
                    }
                    messageDic.Remove(msg.Id);
                    return;
                }
                if (reaction.Emote.Name.Equals(warnEmoji.Name))
                {
                    try
                    {
                        FloofDataContext _floofDb = new FloofDataContext();
                        _floofDb.Add(new Warning
                        {
                            DateAdded = DateTime.Now,
                            Forgiven = false,
                            GuildId = badUser.Guild.Id,
                            Moderator = msg.Author.Id,
                            Reason = $"{moderator.Username}#{moderator.Discriminator} -> Warned by BOT for an inappropriate name",
                            UserId = badUser.Id
                        });
                        _floofDb.SaveChanges();

                        EmbedBuilder builder = new EmbedBuilder();
                        builder.Title = "⚖️ Warn Notification";
                        builder.Description = $"You have recieved a warning in {badUser.Guild.Name}";
                        builder.AddField("Reason", "Warned by BOT for an inappropriate name");
                        builder.Color = Color.DarkOrange;
                        await badUser.SendMessageAsync("", false, builder.Build());

                        await channel.SendMessageAsync($"Got it! I warned {badUser.Username}#{badUser.Discriminator}!");
                    }
                    catch (Exception ex)
                    {
                        await channel.SendMessageAsync("Unable to warn user. Do I have the permissions?");
                        Log.Error("Unable to warn user for bad name: " + ex);
                    }
                    messageDic.Remove(msg.Id);
                    return;
                }
                if (reaction.Emote.Name.Equals(kickEmoji.Name))
                {
                    try
                    {
                        //sends message to user
                        EmbedBuilder builder = new EmbedBuilder();
                        builder.Title = "🥾 Kick Notification";
                        builder.Description = $"You have been Kicked from {badUser.Guild.Name}";
                        builder.AddField("Reason", "Kicked by BOT for an inappropriate name");
                        builder.Color = Color.DarkOrange;
                        await badUser.SendMessageAsync("", false, builder.Build());

                        await badUser.KickAsync($"{badUser.Username}#{badUser.Discriminator} -> Inappropriate Name");

                        await channel.SendMessageAsync($"Got it! I kicked {badUser.Username}#{badUser.Discriminator}!");
                    }
                    catch (Exception ex)
                    {
                        await channel.SendMessageAsync("Unable to kick user. Do I have the permissions?");
                        Log.Error("Unable to kick user for bad name: " + ex);
                    }
                    messageDic.Remove(msg.Id);
                    return;
                }
                if (reaction.Emote.Name.Equals(removeEmoji.Name))
                {
                    try
                    {
                        await badUser.Guild.GetUser(badUser.Id).ModifyAsync(user => user.Nickname = "");
                        await channel.SendMessageAsync($"Got it! I changed {badUser.Username}#{badUser.Discriminator}'s nickname!");
                    }
                    catch (Exception ex)
                    {
                        await channel.SendMessageAsync("Unable to remove their nickname. Do I have the permissions?");
                        Log.Error("Unable to remove nickname for bad name: " + ex);
                    }
                    messageDic.Remove(msg.Id);
                    return;
                }
                else
                    return;
            }
        }

    }
}
