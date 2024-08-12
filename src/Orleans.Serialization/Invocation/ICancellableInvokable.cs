#nullable enable
using System;
using System.Threading.Tasks;

namespace Orleans.Serialization.Invocation
{
    /// <summary>
    /// Represents an invokable that can be canceled
    /// </summary>
    public interface ICancellableInvokable : IInvokable
    {
        /// <summary>
        /// Returns an id that uniquely identifies this invokable
        /// </summary>
        Guid GetCancellableTokenId();
    }
}