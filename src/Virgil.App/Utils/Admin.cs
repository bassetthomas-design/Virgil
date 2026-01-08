using Virgil.Core;

namespace Virgil.App.Utils
{
    public static class Admin
    {
        public static bool IsElevated()
        {
            return ProcessElevation.IsProcessElevated();
        }
    }
}
