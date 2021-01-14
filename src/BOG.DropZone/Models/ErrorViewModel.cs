namespace BOG.DropZone.Models
{
	/// <summary>
	/// Standard error page for the MVC site.
	/// </summary>
	public class ErrorViewModel
    {
        /// <summary>
        /// The id of the request
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// Whether the request id is present
        /// </summary>
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
