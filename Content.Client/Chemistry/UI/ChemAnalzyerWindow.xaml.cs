using System.Linq;
using Content.Client.Stylesheets;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Chemistry.UI
{
    /// <summary>
    /// Client-side UI used to control a <see cref="ChemAnalyzerComponent"/>.
    /// </summary>
    [GenerateTypedNameReferences]
    public sealed partial class ChemAnalyzerWindow : DefaultWindow
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        /// <summary>
        /// Create and initialize the dispenser UI client-side. Creates the basic layout,
        /// actual data isn't filled in until the server sends data about the dispenser.
        /// </summary>
        public ChemAnalyzerWindow()
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);
        }

        /// <summary>
        /// Update the UI state when new state data is received from the server.
        /// </summary>
        /// <param name="state">State data sent by the server.</param>
        public void UpdateState(BoundUserInterfaceState state)
        {
            var castState = (ChemAnalyzerBoundUserInterfaceState) state;
            UpdateContainerInfo(castState);

            EjectButton.Disabled = castState.OutputContainer is null;
        }

        /// <summary>
        /// A handy linebreaker function
        /// </summary>
        public static string SpliceText(string inputText, int lineLength)
        {

            string[] stringSplit = inputText.Split(' ');
            int charCounter = 0;
            string finalString = "";

            for (int i = 0; i < stringSplit.Length; i++)
            {
                finalString += stringSplit[i] + " ";
                charCounter += stringSplit[i].Length;

                if (charCounter > lineLength)
                {
                    finalString += "\n";
                    charCounter = 0;
                }
            }
            return finalString;
        }

        /// <summary>
        /// Update the fill state and list of reagents held by the current reagent container, if applicable.
        /// <para>Also highlights a reagent if it's dispense button is being mouse hovered.</para>
        /// </summary>
        /// <param name="state">State data for the dispenser.</param>
        /// <param name="highlightedReagentId">Prototype ID of the reagent whose dispense button is currently being mouse hovered,
        /// or null if no button is being hovered.</param>
        public void UpdateContainerInfo(ChemAnalyzerBoundUserInterfaceState state, string? highlightedReagentId = null)
        {
            ContainerInfo.Children.Clear();

            if (state.OutputContainer is null)
            {
                ContainerInfo.Children.Add(new Label {Text = Loc.GetString("reagent-dispenser-window-no-container-loaded-text") });
                return;
            }

            ContainerInfo.Children.Add(new BoxContainer // Name of the container and its fill status (Ex: 44/100u)
            {
                Orientation = LayoutOrientation.Horizontal,
                Children =
                {
                    new Label {Text = $"{state.OutputContainer.DisplayName}: "},
                    new Label
                    {
                        Text = $"{state.OutputContainer.CurrentVolume}/{state.OutputContainer.MaxVolume}",
                        StyleClasses = {StyleNano.StyleClassLabelSecondaryColor}
                    }
                }
            });

            foreach (var reagent in state.OutputContainer.Contents)
            {
                // Try get to the prototype for the given reagent. This gives us its description.
                //TODO expand on this to include more details
                var localizedName = _prototypeManager.TryIndex(reagent.Id, out ReagentPrototype? p)
                    ? p.LocalizedName
                    : Loc.GetString("reagent-dispenser-window-reagent-name-not-found-text");

                var localizedDesc = "no description for reagent found";
                var localizedPDesc = "indescribable";

                if (p != null)
                {
                    localizedDesc = p.LocalizedDescription;
                    localizedPDesc = p.LocalizedPhysicalDescription;
                }

                var nameLabel = new Label {
                    Text = $"{localizedName}: ",
                };

                var descriptionLabel = new Label
                {
                    Text = $"{localizedDesc} (Appears {localizedPDesc})",
                };

                var quantityLabel = new Label
                {
                    Text = Loc.GetString("reagent-dispenser-window-quantity-label-text", ("quantity", reagent.Quantity)),
                    StyleClasses = {StyleNano.StyleClassLabelSecondaryColor},
                };

                // Check if the reagent is being moused over. If so, color it green.
                if (reagent.Id == highlightedReagentId) {
                    nameLabel.SetOnlyStyleClass(StyleNano.StyleClassPowerStateGood);
                    quantityLabel.SetOnlyStyleClass(StyleNano.StyleClassPowerStateGood);
                }

                ContainerInfo.Children.Add(new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Children =
                    {
                        nameLabel,
                        quantityLabel,   
                    }
                });

                //TODO make linebreak char count dynamic
                descriptionLabel.Text = SpliceText(descriptionLabel.Text,60);

                ContainerInfo.Children.Add(new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Children =
                    {
                        descriptionLabel,
                    }
                });
                ContainerInfo.Children.Add(new Control
                {
                    MinSize = new Vector2(0, 10),
                });
            }
        }
    }
}