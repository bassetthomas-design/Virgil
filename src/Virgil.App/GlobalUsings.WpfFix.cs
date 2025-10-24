// Global aliases to ensure WPF types are selected over WinForms/Drawing
#pragma warning disable 8019

global using Application = System.Windows.Application;
global using UserControl = System.Windows.Controls.UserControl;

// Media shortcuts for ambiguous types
global using Brush = System.Windows.Media.Brush;
global using SolidColorBrush = System.Windows.Media.SolidColorBrush;
global using Color = System.Windows.Media.Color;

// Optional namespace alias used by some files
global using Media = System.Windows.Media;
