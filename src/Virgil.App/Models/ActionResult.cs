namespace Virgil.App.Models
{
    public record ActionResult(bool Success, string Message)
    {
        public static ActionResult Completed(string message = "") => new(true, message);

        public static ActionResult Failure(string message) => new(false, message);

        public static ActionResult NotImplemented(string message = "Action non implémentée") => new(false, message);
    }
}
