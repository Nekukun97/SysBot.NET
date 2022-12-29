using System;
using Discord;
using Discord.WebSocket;
using PKHeX.Core;
using System.Threading.Tasks;
using SysBot.Base;

namespace SysBot.Pokemon.Discord
{
    public static class AutoLegalityExtensionsDiscord
    {
        public static async Task ReplyWithLegalizedSetAsync(this ISocketMessageChannel channel, ITrainerInfo sav, ShowdownSet set)
        {
            if (set.Species <= 0)
            {
                await channel.SendMessageAsync("Vaya! No fui capaz de interpretar tu mensaje! Si intentas convertir algo, por favor revise dos veces lo que esta pegando!").ConfigureAwait(false);
                return;
            }

            try
            {
                var template = AutoLegalityWrapper.GetTemplate(set);
                var pkm = sav.GetLegal(template, out var result);
                var la = new LegalityAnalysis(pkm);
                var spec = GameInfo.Strings.Species[template.Species];
                if (!la.Valid)
                {
                    var reason = result == "Timeout" ? $"That {spec} set took too long to generate." : $"I wasn't able to create a {spec} from that set.";
                    var imsg = $"Oops! {reason}";
                    if (result == "Failed")
                        imsg += $"\n{AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm)}";
                    await channel.SendMessageAsync(imsg).ConfigureAwait(false);
                    return;
                }

                var msg = $"Aqui esta tu PKM ({result}) legalizado {spec} ({la.EncounterOriginal.Name})!";
                await channel.SendPKMAsync(pkm, msg + $"\n{ReusableActions.GetFormattedShowdownText(pkm)}").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(AutoLegalityExtensionsDiscord));
                var msg = $"Vaya! Ocurrio un error inesperado con este Showdown Set:\n```{string.Join("\n", set.GetSetLines())}```";
                await channel.SendMessageAsync(msg).ConfigureAwait(false);
            }
        }

        public static async Task ReplyWithLegalizedSetAsync(this ISocketMessageChannel channel, string content, int gen)
        {
            content = ReusableActions.StripCodeBlock(content);
            var set = new ShowdownSet(content);
            var sav = AutoLegalityWrapper.GetTrainerInfo(gen);
            await channel.ReplyWithLegalizedSetAsync(sav, set).ConfigureAwait(false);
        }

        public static async Task ReplyWithLegalizedSetAsync<T>(this ISocketMessageChannel channel, string content) where T : PKM, new()
        {
            content = ReusableActions.StripCodeBlock(content);
            var set = new ShowdownSet(content);
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            await channel.ReplyWithLegalizedSetAsync(sav, set).ConfigureAwait(false);
        }

        public static async Task ReplyWithLegalizedSetAsync(this ISocketMessageChannel channel, IAttachment att)
        {
            var download = await NetUtil.DownloadPKMAsync(att).ConfigureAwait(false);
            if (!download.Success)
            {
                await channel.SendMessageAsync(download.ErrorMessage).ConfigureAwait(false);
                return;
            }

            var pkm = download.Data!;
            if (new LegalityAnalysis(pkm).Valid)
            {
                await channel.SendMessageAsync($"{download.SanitizedFileName}: Ahora es legal.").ConfigureAwait(false);
                return;
            }

            var legal = pkm.LegalizePokemon();
            if (!new LegalityAnalysis(legal).Valid)
            {
                await channel.SendMessageAsync($"{download.SanitizedFileName}: No se puede legalizar.").ConfigureAwait(false);
                return;
            }

            legal.RefreshChecksum();

            var msg = $"Aqui esta tu Pokemon legal {download.SanitizedFileName}!\n{ReusableActions.GetFormattedShowdownText(legal)}";
            await channel.SendPKMAsync(legal, msg).ConfigureAwait(false);
        }
    }
}