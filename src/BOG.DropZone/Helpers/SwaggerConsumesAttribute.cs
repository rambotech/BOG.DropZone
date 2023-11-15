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
		/// <summary>
		/// 
		/// </summary>
		/// <param name="contentTypes"></param>
		public SwaggerConsumesAttribute(params string[] contentTypes)
		{
			this.ContentTypes = contentTypes;
		}

		/// <summary>
		/// 
		/// </summary>
		public IEnumerable<string> ContentTypes { get; }
	}
}
