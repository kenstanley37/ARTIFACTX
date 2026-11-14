using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>One branch of a recipe's ingredient tree: the node's tile, then the branches it is made from to its right
/// (this control again, so the tree is as deep as the recipe). Lines are plain borders, no custom drawing.</summary>
public sealed partial class RecipeTreeBranch : UserControl
{
    public RecipeTreeNode? Node
    {
        get => (RecipeTreeNode?)GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }
    public static readonly DependencyProperty NodeProperty = DependencyProperty.Register(
        nameof(Node), typeof(RecipeTreeNode), typeof(RecipeTreeBranch), new PropertyMetadata(null));

    public RecipeTreeBranch() => InitializeComponent();
}
