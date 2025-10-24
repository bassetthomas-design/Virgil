// Global aliases to ensure WPF types are selected over WinForms/Drawing
#pragma warning disable 8019

global using Application = System.Windows.Application;
global using UserControl = System.Windows.Controls.UserControl;

// Prefer WPF for ambiguous types
global using Brush = System.Windows.Media.Brush;
global using Color = System.Windows.Media.Color;

// (No global alias for Media or SolidColorBrush to avoid CS1537 with local aliases)
