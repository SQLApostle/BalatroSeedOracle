using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Oracle.Services;

namespace Oracle.Controls
{
    public partial class ItemConfigPopup : UserControl
    {
        public event EventHandler<ItemConfigEventArgs>? ConfigApplied;
        public event EventHandler? DeleteRequested;
        public event EventHandler? Cancelled;
        
        private string _itemKey = "";
        private bool[] _selectedAntes = new bool[8] { true, true, true, true, true, true, true, true };
        private bool _isJoker = false;
        
        public ItemConfigPopup()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            
            // Initialize after loading
            Dispatcher.UIThread.Post(() => {
                LoadEditionImages();
            });
        }
        
        private void LoadEditionImages()
        {
            var spriteService = SpriteService.Instance;
            
            // Load Normal edition image
            var normalImage = this.Find<Image>("EditionNormalImage");
            if (normalImage != null)
            {
                normalImage.Source = spriteService.GetEditionImage("normal");
            }
            
            // Load Foil edition image
            var foilImage = this.Find<Image>("EditionFoilImage");
            if (foilImage != null)
            {
                foilImage.Source = spriteService.GetEditionImage("foil");
            }
            
            // Load Holographic edition image
            var holoImage = this.Find<Image>("EditionHoloImage");
            if (holoImage != null)
            {
                holoImage.Source = spriteService.GetEditionImage("holographic");
            }
            
            // Load Polychrome edition image
            var polyImage = this.Find<Image>("EditionPolyImage");
            if (polyImage != null)
            {
                polyImage.Source = spriteService.GetEditionImage("polychrome");
            }
            
            // Load Negative edition image
            var negativeImage = this.Find<Image>("EditionNegativeImage");
            if (negativeImage != null)
            {
                negativeImage.Source = spriteService.GetEditionImage("negative");
            }
        }
        
        public void SetItem(string itemKey, string itemName, ItemConfig? existingConfig = null)
        {
            _itemKey = itemKey;
            
            // Check if this is a joker (editions only apply to jokers)
            _isJoker = IsJokerItem(itemKey);
            
            var nameText = this.FindControl<TextBlock>("ItemNameText");
            if (nameText != null)
                nameText.Text = itemName;
            
            // Show/hide edition section based on item type
            var editionBorder = this.FindControl<Border>("EditionSection");
            if (editionBorder != null)
                editionBorder.IsVisible = _isJoker;
            
            if (existingConfig != null)
            {
                // Load existing ante configuration
                if (existingConfig.SearchAntes != null && existingConfig.SearchAntes.Count > 0)
                {
                    // Clear all antes first
                    for (int i = 0; i < 8; i++)
                    {
                        _selectedAntes[i] = false;
                    }
                    
                    // Set selected antes
                    foreach (var ante in existingConfig.SearchAntes)
                    {
                        if (ante >= 1 && ante <= 8)
                        {
                            _selectedAntes[ante - 1] = true;
                        }
                    }
                    
                    UpdateAnteCheckboxes();
                }
                else
                {
                    // Default to all antes selected
                    for (int i = 0; i < 8; i++)
                    {
                        _selectedAntes[i] = true;
                    }
                    UpdateAnteCheckboxes();
                }
                
                // Set edition
                if (!string.IsNullOrEmpty(existingConfig.Edition))
                {
                    switch (existingConfig.Edition.ToLower())
                    {
                        case "foil":
                            SetRadioButton("EditionFoil");
                            break;
                        case "holographic":
                            SetRadioButton("EditionHolo");
                            break;
                        case "polychrome":
                            SetRadioButton("EditionPoly");
                            break;
                        case "negative":
                            SetRadioButton("EditionNegative");
                            break;
                        default:
                            SetRadioButton("EditionNormal");
                            break;
                    }
                }
                
                // Set sources
                if (existingConfig.Sources != null)
                {
                    SetCheckBox("SourceTags", existingConfig.Sources.Contains("tag"));
                    SetCheckBox("SourcePacks", existingConfig.Sources.Contains("booster"));
                    SetCheckBox("SourceShop", existingConfig.Sources.Contains("shop"));
                }
            }
        }
        
        private void SetRadioButton(string name)
        {
            var radio = this.FindControl<RadioButton>(name);
            if (radio != null)
                radio.IsChecked = true;
        }
        
        private void SetCheckBox(string name, bool isChecked)
        {
            var check = this.FindControl<CheckBox>(name);
            if (check != null)
                check.IsChecked = isChecked;
        }
        
        private void OnApplyClick(object? sender, RoutedEventArgs e)
        {
            var config = new ItemConfig
            {
                ItemKey = _itemKey,
                SearchAntes = GetSelectedAntes(),
                Edition = GetSelectedEdition(),
                Sources = GetSelectedSources()
            };
            
            ConfigApplied?.Invoke(this, new ItemConfigEventArgs { Config = config });
        }
        
        private void OnDeleteClick(object? sender, RoutedEventArgs e)
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }
        
        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
        
        private List<int>? GetSelectedAntes()
        {
            var antes = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                if (_selectedAntes[i])
                {
                    antes.Add(i + 1);
                }
            }
            
            // If all antes are selected, return null (means "any ante")
            if (antes.Count == 8)
            {
                return null;
            }
            
            return antes.Count > 0 ? antes : null;
        }
        
        public string GetItem()
        {
            return _itemKey;
        }
        
        private void UpdateAnteCheckboxes()
        {
            for (int i = 0; i < 8; i++)
            {
                var checkbox = this.FindControl<CheckBox>($"Ante{i + 1}");
                if (checkbox != null)
                {
                    checkbox.IsChecked = _selectedAntes[i];
                }
            }
        }
        
        private string GetSelectedEdition()
        {
            if (this.FindControl<RadioButton>("EditionFoil")?.IsChecked == true) return "foil";
            if (this.FindControl<RadioButton>("EditionHolo")?.IsChecked == true) return "holographic";
            if (this.FindControl<RadioButton>("EditionPoly")?.IsChecked == true) return "polychrome";
            if (this.FindControl<RadioButton>("EditionNegative")?.IsChecked == true) return "negative";
            return "none";
        }
        
        private List<string> GetSelectedSources()
        {
            var sources = new List<string>();
            
            if (this.FindControl<CheckBox>("SourceTags")?.IsChecked == true) sources.Add("tag");
            if (this.FindControl<CheckBox>("SourcePacks")?.IsChecked == true) sources.Add("booster");
            if (this.FindControl<CheckBox>("SourceShop")?.IsChecked == true) sources.Add("shop");
            
            // Default to main sources if none selected
            if (sources.Count == 0)
            {
                sources.AddRange(new[] { "tag", "booster", "shop" });
            }
            
            return sources;
        }
        
        private bool IsJokerItem(string itemKey)
        {
            // Check if the item key corresponds to a joker
            // Jokers typically start with specific prefixes or are in joker categories
            return itemKey.Contains("joker") || 
                   itemKey.Contains("Joker") ||
                   itemKey.StartsWith("j_") || // Common joker prefix pattern
                   IsSpecificJoker(itemKey);
        }
        
        private bool IsSpecificJoker(string itemKey)
        {
            // Add specific joker names that might not follow the pattern
            var jokerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "blueprint", "brainstorm", "satellite", "showman", "flower_pot",
                "merry_andy", "oops_all_6s", "the_idol", "seeing_double",
                "matador", "hit_the_road", "the_duo", "the_trio", "the_family",
                "the_order", "the_tribe", "stuntman", "invisible_joker",
                "brainstorm", "satellite", "showman", "flower_pot",
                "blueprint", "wee_joker", "joker", "greedy_joker",
                "lusty_joker", "wrathful_joker", "gluttonous_joker"
                // Add more as needed
            };
            
            return jokerKeys.Contains(itemKey);
        }
        
        private void OnAnteClick(object? sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Name != null)
            {
                // Extract ante number from checkbox name (e.g., "Ante1" -> 1)
                if (checkBox.Name.StartsWith("Ante") && int.TryParse(checkBox.Name.Substring(4), out int anteNum))
                {
                    if (anteNum >= 1 && anteNum <= 8)
                    {
                        _selectedAntes[anteNum - 1] = checkBox.IsChecked == true;
                    }
                }
            }
        }
        
        private void OnEditionClick(object? sender, RoutedEventArgs e)
        {
            // Edition selection is handled by radio button group automatically
            // We'll read the selected edition when Apply is clicked
        }
        
        private void OnSourceClick(object? sender, RoutedEventArgs e)
        {
            // Source selection is handled automatically by checkboxes
            // We'll read the selected sources when Apply is clicked
        }
    }
    
    public class ItemConfigEventArgs : EventArgs
    {
        public ItemConfig Config { get; set; } = new();
    }
    
    public class ItemConfig
    {
        public string ItemKey { get; set; } = "";
        public List<int>? SearchAntes { get; set; }
        public string Edition { get; set; } = "none";
        public List<string> Sources { get; set; } = new();
    }
}