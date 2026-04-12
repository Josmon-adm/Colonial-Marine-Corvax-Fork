using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared._RMC14.Xenonids.Maturing;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Coordinates;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.StatusEffect;
using Robust.Shared.Collections;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Rules.DistressSignal;

public sealed partial class CMDistressSignalRuleSystem
{
    private const int HijackCameraShakeIntensity = 10;
    private const int HijackCameraShakeDuration = 2;

    /// <summary>
    /// Handles the start of a dropship hijack: destroys xeno structures on the planet,
    /// evacuates living xenos to the dropship, and calculates xeno surge based on marine weights.
    /// </summary>
    private void OnDropshipHijackStart(ref DropshipHijackStartEvent ev)
    {
        var hiveStructures = EntityQueryEnumerator<HiveConstructionLimitedComponent, TransformComponent>();
        while (hiveStructures.MoveNext(out var id, out _, out var xform))
        {
            EnsureComp<HiveConstructionSuppressAnnouncementsComponent>(id);

            if (xform.ParentUid != ev.Dropship && _rmcPlanet.IsOnPlanet(id.ToCoordinates()))
                _destruction.DestroyEntity(id);
        }

        var xenoLimitedStructures = EntityQueryEnumerator<XenoSecretionLimitedComponent, TransformComponent>();
        while (xenoLimitedStructures.MoveNext(out var id, out _, out var xform))
        {
            EnsureComp<HiveConstructionSuppressAnnouncementsComponent>(id);

            if (xform.ParentUid != ev.Dropship && _rmcPlanet.IsOnPlanet(id.ToCoordinates()))
                _destruction.DestroyEntity(id);
        }

        //TODO RMC14 only do main hive
        var xenos = EntityQueryEnumerator<XenoComponent, MobStateComponent, TransformComponent>();
        var xenoAmount = 0;
        var larva = 0;
        while (xenos.MoveNext(out var xeno, out var comp, out var mobstate, out var transformComp))
        {
            if (_mobState.IsDead(xeno))
                continue;

            if (transformComp.ParentUid != ev.Dropship && _rmcPlanet.IsOnPlanet(xeno.ToCoordinates()))
            {
                if (comp.CountedInSlots)
                    larva++;

                //Ghost player and send message
                if (TryComp(xeno, out ActorComponent? actor))
                {
                    var session = actor.PlayerSession;
                    Entity<MindComponent> mind;

                    if (_mind.TryGetMind(session, out var mindId, out var mindComp))
                        mind = (mindId, mindComp);
                    else
                        mind = _mind.CreateMind(session.UserId);

                    var ghost = _ghost.SpawnGhost((mind.Owner, mind.Comp), xeno);
                    if (ghost != null)
                        EnsureComp<JoinXenoCooldownIgnoreComponent>(ghost.Value);

                    var origin = _transform.GetMoverCoordinates(xeno);
                    _popup.PopupCoordinates(Loc.GetString("rmc-xeno-hibernation"), origin, Filter.SinglePlayer(session), true, PopupType.MediumXeno);
                }

                QueueDel(xeno);
            }
            else
                xenoAmount++;
        }

        //Queen Maturing - TODO only main hive
        var queens = EntityQueryEnumerator<XenoMaturingComponent, MobStateComponent>();
        while (queens.MoveNext(out var queen, out var maturing, out var mobstate))
        {
            if (_mobState.IsDead(queen))
                continue;

            _maturing.Mature((queen, maturing));
        }

        //Surge
        var shipQuery = EntityQueryEnumerator<MarineComponent, MobStateComponent, InfectableComponent, TransformComponent>();

        float totalHostWeights = 0;
        while (shipQuery.MoveNext(out var marine, out var mob, out var _, out var _, out var transformComp))
        {
            if (_mobState.IsDead(marine) || !_almayerMaps.Contains(transformComp.MapID))
                continue;

            if (!TryComp<MindContainerComponent>(marine, out var mindContainer))
                continue;

            if (mindContainer is null ||
                !TryComp<MindComponent>(mindContainer.Mind, out var mind))
                continue;

            foreach (var roleId in mind.MindRoles)
            {
                if (!TryComp<MindRoleComponent>(roleId, out var mindRole))
                    continue;

                if (mindRole.JobPrototype == null || !_prototypes.TryIndex(mindRole.JobPrototype, out var proto))
                    continue;

                totalHostWeights += proto.RoleWeight;
            }
        }

        //Get the maximum of either remaining marines or minimum amount
        var surgeAmount = Math.Max((int)Math.Ceiling(totalHostWeights * _hijackShipWeight) - xenoAmount, _hijackMinBurrowed);
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            // Reset Hivecore Cooldown
            var hiveComp = EnsureComp<HiveComponent>(rule.Hive);
            //Add all the stranded xenos up
            _hive.IncreaseBurrowedLarva(larva); // TODO RMC14 should prob make sure it's only main hive
            _hive.ResetHiveCoreCooldown((rule.Hive, hiveComp));
            var surge = EnsureComp<HijackBurrowedSurgeComponent>(rule.Hive);
            surge.PooledLarva = surgeAmount;
        }
    }

    /// <summary>
    /// Handles the dropship landing after hijack: plays the hijack song, shakes cameras,
    /// and paralyzes all marines on the Almayer.
    /// </summary>
    private void OnDropshipHijackLanded(ref DropshipHijackLandedEvent ev)
    {
        var rules = QueryActiveRules();
        var time = Timing.CurTime;
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (rule.HijackSongPlayed)
                break;

            rule.HijackSongPlayed = true;
            var song = _audio.PlayGlobal(rule.HijackSong, Filter.Broadcast(), true);
            if (song?.Entity is { } songEnt)
                EnsureComp<RMCHijackSongComponent>(songEnt);

            rule.ForceEndAt = time + _forceEndHijackTime;
            break;
        }

        var didCameraShake = false;
        var warshipQuery = EntityQueryEnumerator<AlmayerComponent, TransformComponent>();
        while (warshipQuery.MoveNext(out var uid, out _, out var xform)) // Stun everyone on the Almayer
        {
            if (!didCameraShake)
            {
                var map = _transform.GetMapId(uid);
                var sameMap = Filter.BroadcastMap(map);
                _rmcCameraShake.ShakeCamera(sameMap, HijackCameraShakeIntensity, HijackCameraShakeDuration);
                didCameraShake = true;
            }

            StunAllMarinesOnAlmayer(xform);
        }
    }

    /// <summary>
    /// Stuns all marines on the Almayer.
    /// </summary>
    private void StunAllMarinesOnAlmayer(TransformComponent xform)
    {
        // Get enumeration exceptions from people dropping things if we just paralyze as we go
        var toKnock = new ValueList<EntityUid>();
        GetMarinesOnAlmayer(xform, ref toKnock);

        foreach (var child in toKnock)
        {
            if (!TryComp<StatusEffectsComponent>(child, out var status))
                continue;

            _stuns.TryParalyze(child, _hijackStunTime, true, status);
        }
    }

    /// <summary>
    /// Gets all marines on the Almayer.
    /// </summary>
    private void GetMarinesOnAlmayer(TransformComponent xform, ref ValueList<EntityUid> reference)
    {
        // Not recursive because probably not necessary? If we need it to be that's why this method is separate.
        var childEnumerator = xform.ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            if (HasComp<XenoComponent>(child))
                continue;

            reference.Add(child);
        }
    }
}
