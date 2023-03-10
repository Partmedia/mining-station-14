using System.Linq;
using Content.Client.Stylesheets;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Client.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Chemistry.UI
{
    /// <summary>
    /// Client-side UI used to control a <see cref="SharedBiopressComponent"/>
    /// </summary>
    [GenerateTypedNameReferences]
    public sealed partial class BiopressWindow : DefaultWindow
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        public event Action<BaseButton.ButtonEventArgs, BiopressReagentButton>? OnBiopressReagentButtonPressed;

        /// <summary>
        /// Create and initialize the UI client-side.
        /// </summary>
        public BiopressWindow()
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);
        }

        private BiopressReagentButton MakeBiopressReagentButton(string text, BiopressReagentAmount amount, string id, bool isBuffer, string styleClass)
        {
            var button = new BiopressReagentButton(text, amount, id, isBuffer, styleClass);
            button.OnPressed += args
                => OnBiopressReagentButtonPressed?.Invoke(args, button);
            return button;
        }

        /// <summary>
        /// Update the UI state when new state data is received from the server.
        /// </summary>
        /// <param name="state">State data sent by the server.</param>
        public void UpdateState(BoundUserInterfaceState state)
        {
            var castState = (BiopressBoundUserInterfaceState) state;
            
            UpdatePanelInfo(castState);

            var output = castState.OutputContainerInfo;
            OutputEjectButton.Disabled = output is null;

            var remainingCapacity = output is null ? 0 : (output.MaxVolume - output.CurrentVolume).Int();
            var holdsReagents = output?.HoldsReagents ?? false;
        }

        /// <summary>
        /// Update the container, buffer, and output panels.
        /// </summary>
        /// <param name="state">State data for the dispenser.</param>
        private void UpdatePanelInfo(BiopressBoundUserInterfaceState state)
        {
            BufferTransferButton.Pressed = state.Mode == Shared.Chemistry.BiopressMode.Transfer;
            BufferDiscardButton.Pressed = state.Mode == Shared.Chemistry.BiopressMode.Discard;

            BuildContainerUI(OutputContainerInfo, state.OutputContainerInfo);

            BufferInfo.Children.Clear();

            if (!state.BufferReagents.Any())
            {
                BufferInfo.Children.Add(new Label { Text = Loc.GetString("chem-master-window-buffer-empty-text") });

                return;
            }

            var bufferHBox = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal
            };
            BufferInfo.AddChild(bufferHBox);

            var bufferLabel = new Label { Text = $"{Loc.GetString("chem-master-window-buffer-label")} " };
            bufferHBox.AddChild(bufferLabel);
            var bufferVol = new Label
            {
                Text = $"{state.BufferCurrentVolume}u",
                StyleClasses = {StyleNano.StyleClassLabelSecondaryColor}
            };
            bufferHBox.AddChild(bufferVol);

            foreach (var reagent in state.BufferReagents)
            {
                // Try to get the prototype for the given reagent.
                _prototypeManager.TryIndex(reagent.ReagentId, out ReagentPrototype? proto);
                var name = proto?.LocalizedPhysicalDescription ?? Loc.GetString("chem-master-window-unknown-reagent-text");

                if (proto != null)
                {
                    BufferInfo.Children.Add(new BoxContainer
                    {
                        Orientation = LayoutOrientation.Horizontal,
                        Children =
                        {
                            new Label {Text = $"{name}: "},
                            new Label
                            {
                                Text = $"{reagent.Quantity}u",
                                StyleClasses = {StyleNano.StyleClassLabelSecondaryColor}
                            },

                            // Padding
                            new Control {HorizontalExpand = true},

                            MakeBiopressReagentButton("1", BiopressReagentAmount.U1, reagent.ReagentId, true, StyleBase.ButtonOpenRight),
                            MakeBiopressReagentButton("5", BiopressReagentAmount.U5, reagent.ReagentId, true, StyleBase.ButtonOpenBoth),
                            MakeBiopressReagentButton("10", BiopressReagentAmount.U10, reagent.ReagentId, true, StyleBase.ButtonOpenBoth),
                            MakeBiopressReagentButton("25", BiopressReagentAmount.U25, reagent.ReagentId, true, StyleBase.ButtonOpenBoth),
                            MakeBiopressReagentButton(Loc.GetString("chem-master-window-buffer-all-amount"), BiopressReagentAmount.All, reagent.ReagentId, true, StyleBase.ButtonOpenLeft),
                        }
                    });
                }
            }
        }

        private void BuildContainerUI(Control control, BiopressContainerInfo? info)
        {
            control.Children.Clear();

            if (info is null)
            {
                control.Children.Add(new Label
                {
                    Text = Loc.GetString("chem-master-window-no-container-loaded-text")
                });
            }
            else
            {
                // Name of the container and its fill status (Ex: 44/100u)
                control.Children.Add(new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Children =
                    {
                        new Label {Text = $"{info.DisplayName}: "},
                        new Label
                        {
                            Text = $"{info.CurrentVolume}/{info.MaxVolume}",
                            StyleClasses = {StyleNano.StyleClassLabelSecondaryColor}
                        }
                    }
                });

                var contents = info.Contents
                    .Select(lineItem =>
                    {
                        if (!info.HoldsReagents)
                            return (lineItem.Id, lineItem.Id, lineItem.Quantity);

                        // Try to get the prototype for the given reagent.
                        _prototypeManager.TryIndex(lineItem.Id, out ReagentPrototype? proto);
                        var name = proto?.LocalizedPhysicalDescription
                                   ?? Loc.GetString("chem-master-window-unknown-reagent-text");

                        return (name, lineItem.Id, lineItem.Quantity);

                    })
                    .OrderBy(r => r.Item1);

                foreach (var (name, id, quantity) in contents)
                {
                    var inner = new BoxContainer
                    {
                        Orientation = LayoutOrientation.Horizontal,
                        Children =
                        {
                            new Label { Text = $"{name}: " },
                            new Label
                            {
                                Text = $"{quantity}u",
                                StyleClasses = { StyleNano.StyleClassLabelSecondaryColor },
                            }
                        }
                    };

                    control.Children.Add(inner);
                }

            }
        }
    }

    public sealed class BiopressReagentButton : Button
    {
        public BiopressReagentAmount Amount { get; set; }
        public bool IsBuffer = true;
        public string Id { get; set; }
        public BiopressReagentButton(string text, BiopressReagentAmount amount, string id, bool isBuffer, string styleClass)
        {
            AddStyleClass(styleClass);
            Text = text;
            Amount = amount;
            Id = id;
            IsBuffer = isBuffer;
        }
    }
}
