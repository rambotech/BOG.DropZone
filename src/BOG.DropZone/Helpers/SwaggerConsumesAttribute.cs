using System.Collections.Generic;
using System;

namespace BOG.DropZone.Helpers
{
	/// <summary>
	/// Needed for controlling the Request Body dropdown content in the Swagger display.
	/// </summary>
	
	[AttributeUsage(AttributeTargets.Method)]
	public class SwaggerConsumesAttribute : Attribute
	{
		public SwaggerConsumesAttribute(params string[] contentTypes)
		{
			this.ContentTypes = contentTypes;
		}

		public IEnumerable<string> ContentTypes { get; }
	}
}
