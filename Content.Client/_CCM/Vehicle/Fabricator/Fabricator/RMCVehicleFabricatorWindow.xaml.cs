using Content.Client.Message;
using Content.Shared._CCM.Vehicle.Fabricator;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;

namespace Content.Client._CCM.Vehicle.Fabricator.Fabricator;

public sealed class RMCVehicleFabricatorWindow : DefaultWindow
{
    public event Action<RMCVehicleFabricatorCategory>? OnCategorySelected;
    public event Action<RMCVehicleType>? OnVehicleSelected;
    public event Action<EntProtoId>? OnPrint;

    public RMCVehicleFabricatorCategory SelectedCategory { get; private set; } = RMCVehicleFabricatorCategory.Primary;
    public RMCVehicleType SelectedVehicle { get; private set; } = RMCVehicleType.Tank;

    private Label PointsLabel => FindControl<Label>("PointsLabel");
    private Label PrintingLabel => FindControl<Label>("PrintingLabel");
    private BoxContainer PrintablesContainer => FindControl<BoxContainer>("PrintablesContainer");
    private RichTextLabel WindowTitleLabel => FindControl<RichTextLabel>("WindowTitleLabel");
    private RichTextLabel CategoryTitleLabel => FindControl<RichTextLabel>("CategoryTitleLabel");

    public RMCVehicleFabricatorWindow()
    {
        RobustXamlLoader.Load(this);

        WindowTitleLabel.SetMarkupPermissive($"[color=#3db83b][bold]{Loc.GetString("rmc-vehicle-fabricator-window-title")}[/bold][/color]");

        var primaryBtn = FindControl<Button>("PrimaryCategoryButton");
        var secondaryBtn = FindControl<Button>("SecondaryCategoryButton");
        var armorBtn = FindControl<Button>("ArmorCategoryButton");
        var supportBtn = FindControl<Button>("SupportCategoryButton");
        var chassisBtn = FindControl<Button>("ChassisCategoryButton");
        var ammoBtn = FindControl<Button>("AmmoCategoryButton");

        var tankBtn = FindControl<Button>("TankVehicleButton");
        var apcBtn = FindControl<Button>("APCVehicleButton");
        var humveeBtn = FindControl<Button>("HumveeVehicleButton");

        primaryBtn.OnPressed += _ => { SelectedCategory = RMCVehicleFabricatorCategory.Primary; OnCategorySelected?.Invoke(SelectedCategory); UpdateCategoryButtons(primaryBtn); };
        secondaryBtn.OnPressed += _ => { SelectedCategory = RMCVehicleFabricatorCategory.Secondary; OnCategorySelected?.Invoke(SelectedCategory); UpdateCategoryButtons(secondaryBtn); };
        armorBtn.OnPressed += _ => { SelectedCategory = RMCVehicleFabricatorCategory.Armor; OnCategorySelected?.Invoke(SelectedCategory); UpdateCategoryButtons(armorBtn); };
        supportBtn.OnPressed += _ => { SelectedCategory = RMCVehicleFabricatorCategory.Support; OnCategorySelected?.Invoke(SelectedCategory); UpdateCategoryButtons(supportBtn); };
        chassisBtn.OnPressed += _ => { SelectedCategory = RMCVehicleFabricatorCategory.Chassis; OnCategorySelected?.Invoke(SelectedCategory); UpdateCategoryButtons(chassisBtn); };
        ammoBtn.OnPressed += _ => { SelectedCategory = RMCVehicleFabricatorCategory.Ammo; OnCategorySelected?.Invoke(SelectedCategory); UpdateCategoryButtons(ammoBtn); };

        tankBtn.OnPressed += _ => { SelectedVehicle = RMCVehicleType.Tank; OnVehicleSelected?.Invoke(SelectedVehicle); UpdateVehicleButtons(tankBtn); };
        apcBtn.OnPressed += _ => { SelectedVehicle = RMCVehicleType.APC; OnVehicleSelected?.Invoke(SelectedVehicle); UpdateVehicleButtons(apcBtn); };
        humveeBtn.OnPressed += _ => { SelectedVehicle = RMCVehicleType.Humvee; OnVehicleSelected?.Invoke(SelectedVehicle); UpdateVehicleButtons(humveeBtn); };

        tankBtn.Pressed = true;
        primaryBtn.Pressed = true;
        UpdateCategoryTitle();
    }

    private void UpdateCategoryButtons(Button pressed)
    {
        var categories = new[] { "Primary", "Secondary", "Armor", "Support", "Chassis", "Ammo" };
        foreach (var name in categories)
        {
            var btn = FindControl<Button>($"{name}CategoryButton");
            btn.Pressed = btn == pressed;
        }
        UpdateCategoryTitle();
    }

    private void UpdateVehicleButtons(Button pressed)
    {
        var vehicles = new[] { "Tank", "APC", "Humvee" };
        foreach (var name in vehicles)
        {
            var btn = FindControl<Button>($"{name}VehicleButton");
            btn.Pressed = btn == pressed;
        }
        UpdateCategoryTitle();
    }

    private void UpdateCategoryTitle()
    {
        var vehicle = GetVehicleKey(SelectedVehicle);
        var category = GetCategoryKey(SelectedCategory);
        var vehicleLoc = Loc.GetString($"rmc-vehicle-fabricator-vehicle-{vehicle}");
        var categoryLoc = Loc.GetString($"rmc-vehicle-fabricator-category-{category}");
        CategoryTitleLabel.SetMarkupPermissive($"[bold]{vehicleLoc} - {categoryLoc}[/bold]");
    }

    private static string GetVehicleKey(RMCVehicleType type) => type switch
    {
        RMCVehicleType.Tank => "tank",
        RMCVehicleType.APC => "apc",
        RMCVehicleType.Humvee => "humvee",
        _ => type.ToString().ToLowerInvariant(),
    };

    private static string GetCategoryKey(RMCVehicleFabricatorCategory category) => category switch
    {
        RMCVehicleFabricatorCategory.Primary => "primary",
        RMCVehicleFabricatorCategory.Secondary => "secondary",
        RMCVehicleFabricatorCategory.Armor => "armor",
        RMCVehicleFabricatorCategory.Support => "support",
        RMCVehicleFabricatorCategory.Chassis => "chassis",
        RMCVehicleFabricatorCategory.Ammo => "ammo",
        _ => category.ToString().ToLowerInvariant(),
    };

    public void SetPoints(int points)
    {
        PointsLabel.Text = Loc.GetString("rmc-vehicle-fabricator-points", ("points", points));
    }

    public void SetPrinting(string? itemName)
    {
        PrintingLabel.Visible = itemName != null;
        PrintingLabel.Text = itemName != null
            ? Loc.GetString("rmc-vehicle-fabricator-printing", ("item", itemName))
            : string.Empty;
    }

    public void SetCategory(RMCVehicleFabricatorCategory category)
    {
        SelectedCategory = category;
        var categoryNames = new[] { "Primary", "Secondary", "Armor", "Support", "Chassis", "Ammo" };
        foreach (var name in categoryNames)
        {
            var btn = FindControl<Button>($"{name}CategoryButton");
            btn.Pressed = GetCategoryKey(category) == name.ToLowerInvariant();
        }
        UpdateCategoryTitle();
    }

    public void SetVehicle(RMCVehicleType vehicle)
    {
        SelectedVehicle = vehicle;
        var vehicleNames = new[] { "Tank", "APC", "Humvee" };
        foreach (var name in vehicleNames)
        {
            var btn = FindControl<Button>($"{name}VehicleButton");
            btn.Pressed = GetVehicleKey(vehicle) == name.ToLowerInvariant();
        }
        UpdateCategoryTitle();
    }

    public void SetPrintables(List<RMCVehicleFabricatorPrintableDisplayData> printables)
    {
        PrintablesContainer.DisposeAllChildren();

        foreach (var printable in printables)
        {
            var box = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Margin = new Thickness(4, 4),
                HorizontalExpand = true,
            };

            var labelBox = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true
            };

            labelBox.AddChild(new Label
            {
                Text = printable.Name,
                FontColorOverride = Color.FromHex("#FFFFFF"),
            });

            labelBox.AddChild(new Label
            {
                Text = printable.Description,
                FontColorOverride = Color.FromHex("#AAAAAA"),
            });

            box.AddChild(labelBox);

            var button = new Button 
            { 
                Text = Loc.GetString("rmc-vehicle-fabricator-print", ("cost", printable.Cost)),
                MinWidth = 150,
                StyleClasses = { "OpenBoth" }
            };
            button.OnPressed += _ => OnPrint?.Invoke(printable.Id);
            box.AddChild(button);

            PrintablesContainer.AddChild(box);
        }
    }
}
