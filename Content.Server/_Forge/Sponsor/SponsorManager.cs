using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._CCM.Sponsorship;
using Content.Shared._Forge.Sponsor;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Forge.Sponsor;

[UsedImplicitly]
public sealed class SponsorManager : ISharedSponsorManager
{
    // Forge: the sole source of sponsorship is the Discord-role driven level resolved by
    // DiscordAuthManager. The former CCM bridge (CCMSponsorshipManager) has been folded in
    // here so the CCM perk stack (chat colors/tags, armor/xeno/ghost skins, round-end
    // credits, role-timer bypass) keeps working while there is exactly one source of truth.
    private const string DonateUrl = "https://boosty.to/corvaxforge";

    public readonly Dictionary<NetUserId, SponsorLevel> Sponsors = new();

    [Dependency] private readonly IPlayerManager _players = default!;

    public event Action<NetUserId>? SponsorChanged;

    public void Initialize() { }

    public bool TryGetSponsor(NetUserId user, [NotNullWhen(true)] out SponsorLevel level)
    {
        return Sponsors.TryGetValue(user, out level);
    }

    public bool TryGetSponsorColor(SponsorLevel level, [NotNullWhen(true)] out string? color)
    {
        return SponsorData.SponsorColor.TryGetValue(level, out color);
    }

    public bool TryGetSponsorGhost(SponsorLevel level, [NotNullWhen(true)] out string? ghost)
    {
        return SponsorData.SponsorGhost.TryGetValue(level, out ghost);
    }

    public void SetSponsor(NetUserId user, SponsorLevel level)
    {
        if (level == SponsorLevel.None)
        {
            if (Sponsors.Remove(user))
                SponsorChanged?.Invoke(user);
            return;
        }

        Sponsors[user] = level;
        SponsorChanged?.Invoke(user);
    }

    public void RemoveSponsor(NetUserId user)
    {
        if (Sponsors.Remove(user))
            SponsorChanged?.Invoke(user);
    }

    // --- CCM perk tier resolution (folded in from the former CCMSponsorshipManager) ---

    public CCMSponsorshipTier GetTier(NetUserId userId)
    {
        return TryGetSponsor(userId, out var level) ? SponsorLevelToTier(level) : CCMSponsorshipTier.None;
    }

    public CCMSponsorshipStatusSnapshot GetStatus(NetUserId userId)
    {
        if (!TryGetSponsor(userId, out var level) || level == SponsorLevel.None)
            return EmptySnapshot();

        var tier = SponsorLevelToTier(level);
        var oocColor = SponsorData.SponsorColor.GetValueOrDefault(level, GetDefaultColor(tier, false));

        return new CCMSponsorshipStatusSnapshot(
            tier,
            DonateUrl,
            // Discord-role sponsorship is re-checked on every connect, so there is no
            // meaningful expiration to show; 0 renders as "permanent" on the client.
            0,
            oocColor,
            GetDefaultColor(tier, true),
            tier >= CCMSponsorshipTier.SponsorII);
    }

    public bool HasRoleTimerBypass(NetUserId userId)
    {
        return GetTier(userId) >= CCMSponsorshipTier.SponsorII;
    }

    public bool HasRoleTimerBypass(ICommonSession session)
    {
        return HasRoleTimerBypass(session.UserId);
    }

    public bool TryGetChatColorHex(NetUserId userId, bool looc, out string colorHex)
    {
        var status = GetStatus(userId);
        colorHex = looc ? status.LoocColorHex : status.OocColorHex;
        return !string.IsNullOrWhiteSpace(colorHex);
    }

    public IReadOnlyList<(string Ckey, CCMSponsorshipTier Tier)> GetConnectedSponsorsForCredits()
    {
        return _players.Sessions
            .Select(session => (session.Name, Tier: GetTier(session.UserId)))
            .Where(entry => entry.Tier != CCMSponsorshipTier.None)
            .OrderByDescending(entry => entry.Tier)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => (entry.Name, entry.Tier))
            .ToList();
    }

    public static CCMSponsorshipTier SponsorLevelToTier(SponsorLevel level)
    {
        // Перк-распределение (см. CCMSponsorshipWindow):
        //   L1     -> SponsorI   (приоритетный вход + цвет OOC + ckey в конце раунда)
        //   L2     -> SponsorII  (+ цвет LOOC + готовый OOC-тег + базовая кастомизация)
        //   L3..L6 -> SponsorIII (+ скин призрака + свой OOC-тег + расширенная кастомизация)
        return level switch
        {
            SponsorLevel.None => CCMSponsorshipTier.None,
            SponsorLevel.Level1 => CCMSponsorshipTier.SponsorI,
            SponsorLevel.Level2 => CCMSponsorshipTier.SponsorII,
            SponsorLevel.Level3 => CCMSponsorshipTier.SponsorIII,
            SponsorLevel.Level4 => CCMSponsorshipTier.SponsorIII,
            SponsorLevel.Level5 => CCMSponsorshipTier.SponsorIII,
            SponsorLevel.Level6 => CCMSponsorshipTier.SponsorIII,
            _ => CCMSponsorshipTier.None,
        };
    }

    private static CCMSponsorshipStatusSnapshot EmptySnapshot()
    {
        return new CCMSponsorshipStatusSnapshot(
            CCMSponsorshipTier.None,
            DonateUrl,
            0,
            string.Empty,
            string.Empty,
            false);
    }

    private static string GetDefaultColor(CCMSponsorshipTier tier, bool looc)
    {
        return tier switch
        {
            CCMSponsorshipTier.SponsorI => looc ? "#7FD7FF" : "#61C9FF",
            CCMSponsorshipTier.SponsorII => looc ? "#F2A7FF" : "#D96CFF",
            CCMSponsorshipTier.SponsorIII => looc ? "#FFE082" : "#F6C453",
            _ => string.Empty,
        };
    }
}
