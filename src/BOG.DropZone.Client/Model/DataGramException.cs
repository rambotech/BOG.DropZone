using System;

namespace BOG.DropZone.Client.Model
{
	//
	// Summary:
	//     A base class for exceptions thrown by the BOG.DropZone.Client.Model.DataGramException class.
	//     classes.
	public class DataGramException : Exception
    {
        //
        // Summary:
        //     Initializes a new instance of the BOG.DropZone.Client.Model.DataGramException class.
        public DataGramException() : this(null, null)
        {
        }

        //
        // Summary:
        //     Initializes a new instance of the BOG.DropZone.Client.Model.DataGramException class
        //     with a specific message that describes the current exception.
        //
        // Parameters:
        //   message:
        //     A message that describes the current exception.
        public DataGramException(string message) : this(message, null)
        {
        }

        //
        // Summary:
        //     Initializes a new instance of the BOG.DropZone.Client.Model.DataGramException class
        //     with a specific message that describes the current exception and an inner exception.
        //
        // Parameters:
        //   message:
        //     A message that describes the current exception.
        //
        //   inner:
        //     The inner exception.
        public DataGramException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
