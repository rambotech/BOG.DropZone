using System;
using System.IO;
using System.Reflection;
using BOG.DropZone.Interface;
using Newtonsoft.Json.Linq;

namespace BOG.DropZone
{
	/// <summary>
	/// Creates a class providing information about the main assembly.
	/// </summary>
	public class AssemblyVersion : IAssemblyVersion
	{
		/// <summary>
		/// The main file which is the entry point
		/// </summary>
		public string Filename { get; private set; }

		/// <summary>
		/// The application name
		/// </summary>
		public string Name { get; private set; }

		/// <summary>
		/// The build version running
		/// </summary>
		public string Version { get; private set; }

		/// <summary>
		/// The build date of the assembly
		/// </summary>
		public DateTime BuildDate { get; private set; }

		/// <summary>
		/// Instantiator
		/// </summary>
		public AssemblyVersion()
		{
			var av = Assembly.GetEntryAssembly().GetName();
			var FullName = av.CodeBase.Replace("file:///", string.Empty);
			Filename = Path.Combine(Path.GetDirectoryName(FullName), Path.GetFileName(FullName));
			Name = av.Name;
			Version = av.Version.ToString();
			BuildDate = File.GetCreationTime(Filename);
		}

		/// <summary>
		/// Build a simple default string format for the details.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return $"{Path.GetFileName(Filename)}, {Version}, {BuildDate.ToString("G")}";
		}

		/// <summary>
		/// Build a simple default string format for the details.
		/// </summary>
		/// <returns></returns>
		public string ToJson()
		{
			return (new JObject
			{
				{ "name", Name },
				{ "version", Version },
				{ "built", BuildDate.ToString("G") }
			}).ToString(Newtonsoft.Json.Formatting.Indented);
		}
	}
}
