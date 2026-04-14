using Content.Shared._CCM.Miners.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Requisitions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRequisitionsSystem), typeof(SharedMinerSystem))]
public sealed partial class RequisitionsCrateComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Reward = 200;
}
