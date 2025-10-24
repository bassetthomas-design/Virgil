// Global aliases to disambiguate WPF vs WinForms / Drawing types
// This avoids CS0104 ambiguities without touching each file.

#pragma warning disable 8019 // Unused using (aliases used by other files)

// Prefer WPF types by default
global using Application = System.Windows.Application;
global using UserControl = System.Windows.Controls.UserControl;

// Media types
global using Brush = System.Windows.Media.Brush;
global using SolidColorBrush = System.Windows.Media.SolidColorBrush;
global using Color = System.Windows.Media.Color;

// Optional short alias for namespace if needed in code
global using Media = System.Windows.Media;

// NOTE: If a specific file really needs WinForms Application or UserControl,
// it can use fully-qualified names: System.Windows.Forms.Application,
// System.Windows.Forms.UserControl, or add a local alias in that file.
