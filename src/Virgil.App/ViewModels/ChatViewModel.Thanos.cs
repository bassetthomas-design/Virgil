namespace Virgil.App.ViewModels
{
    public partial class ChatViewModel
    {
        public async void SnapAll()
        {
            await _chat.ClearHistoryAsync(applyThanosEffect: true).ConfigureAwait(false);
        }
    }
}
