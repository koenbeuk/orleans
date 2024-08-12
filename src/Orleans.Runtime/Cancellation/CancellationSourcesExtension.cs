using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;
using Orleans.Serialization.Invocation;

namespace Orleans.Runtime
{
    /// <summary>
    /// Contains list of cancellation token source corresponding to the tokens
    /// passed to the related grain activation.
    /// </summary>
    internal class CancellationSourcesExtension : ICancellationSourcesExtension, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IGrainCancellationTokenRuntime _cancellationTokenRuntime;
        private readonly Timer _cleanupTimer;
        private readonly Func<Guid, Entry<GrainCancellationToken>> _createGrainCancellationTokenEntry;
        private readonly Func<Guid, Entry<CancellationTokenSource>> _createCancellationTokenEntry;
        private static readonly TimeSpan _cleanupFrequency = TimeSpan.FromMinutes(7);

        private ConcurrentDictionary<Guid, Entry<GrainCancellationToken>> _grainCancellationTokens;
        private ConcurrentDictionary<Guid, Entry<CancellationTokenSource>> _cancellationTokens;

        /// <summary>
        /// Initializes a new instance of the <see cref="CancellationSourcesExtension"/> class.
        /// </summary>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="cancellationRuntime">The cancellation runtime.</param>
        public CancellationSourcesExtension(ILoggerFactory loggerFactory, IGrainCancellationTokenRuntime cancellationRuntime)
        {
            _logger = loggerFactory.CreateLogger<CancellationSourcesExtension>();
            _cancellationTokenRuntime = cancellationRuntime;
            _cleanupTimer = new Timer(obj => ((CancellationSourcesExtension)obj).ExpireTokens(), this, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            _createGrainCancellationTokenEntry = id => new Entry<GrainCancellationToken>(new GrainCancellationToken(id, false, _cancellationTokenRuntime));
            _createCancellationTokenEntry = id => new Entry<CancellationTokenSource>(new CancellationTokenSource());
        }

        /// <inheritdoc />
        public Task CancelRemoteToken(Guid tokenId)
        {
            _grainCancellationTokens ??= new ConcurrentDictionary<Guid, Entry<GrainCancellationToken>>();

            if (!_grainCancellationTokens.TryGetValue(tokenId, out var entry))
            {
                _logger.LogWarning((int)ErrorCode.CancellationTokenCancelFailed, "Received a cancel call for token with id {TokenId}, but the token was not found", tokenId);

                // Record the cancellation anyway, in case the call which would have registered the cancellation is still pending.
                this.RecordGrainCancellationToken(tokenId, isCancellationRequested: true);
                return Task.CompletedTask;
            }

            entry.Touch();
            var token = entry.Token;
            return token.Cancel();
        }

        /// <inheritdoc />
        public Task CancelInvokable(Guid tokenId)
        {
            _cancellationTokens ??= new ConcurrentDictionary<Guid, Entry<CancellationTokenSource>>();

            if (!_cancellationTokens.TryGetValue(tokenId, out var entry))
            {
                _logger.LogWarning((int)ErrorCode.CancellationTokenCancelFailed, "Received a cancel call for token with id {TokenId}, but the token was not found", tokenId);

                // Record the cancellation anyway, in case the call which would have registered the cancellation is still pending.
                this.RecordCancellationToken(tokenId, isCancellationRequested: true);
                return Task.CompletedTask;
            }

            entry.Touch();
            var token = entry.Token;
            token.Cancel();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Adds <see cref="CancellationToken"/> to the grain extension so that it can be canceled through remote call to the CancellationSourcesExtension.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="request"></param>
        internal static void RegisterCancellationTokens(
            IGrainContext target,
            IInvokable request)
        {
            RegisterCancellationToken(target, request);
            RegisterGrainCancellationTokens(target, request);

            static void RegisterCancellationToken(IGrainContext target, IInvokable request)
            {
                if (request is not ICancellableInvokable cancellableInvokable)
                {
                    // This is not a cancellable request, so we don't need to do anything.
                    return;
                }

                var argumentCount = request.GetArgumentCount();
                for (var i = 0; i < argumentCount; i++)
                {
                    var arg = request.GetArgument(i);
                    if (arg is CancellationToken cancellationToken)
                    {
                        var cancellationExtension = (CancellationSourcesExtension)target.GetGrainExtension<ICancellationSourcesExtension>();

                        // Replacing the half baked CancellationToken that came from the wire with locally fully created one.
                        var tokenId = cancellableInvokable.GetCancellableTokenId();
                        request.SetArgument(i, cancellationExtension.RecordCancellationToken(tokenId, cancellationToken.IsCancellationRequested).Token);

                        // We found the cancellation token, so we can stop looking.
                        return; 
                    }
                }
            }

            static void RegisterGrainCancellationTokens(IGrainContext target, IInvokable request)
            {
                var argumentCount = request.GetArgumentCount();
                for (var i = 0; i < argumentCount; i++)
                {
                    var arg = request.GetArgument(i);
                    if (arg is GrainCancellationToken grainToken)
                    {
                        var cancellationExtension = (CancellationSourcesExtension)target.GetGrainExtension<ICancellationSourcesExtension>();

                        // Replacing the half baked GrainCancellationToken that came from the wire with locally fully created one.
                        request.SetArgument(i, cancellationExtension.RecordGrainCancellationToken(grainToken.Id, grainToken.IsCancellationRequested));
                    }
                }
            }
        }

        private GrainCancellationToken RecordGrainCancellationToken(Guid tokenId, bool isCancellationRequested)
        {
            _grainCancellationTokens ??= new ConcurrentDictionary<Guid, Entry<GrainCancellationToken>>();

            if (_grainCancellationTokens.TryGetValue(tokenId, out var entry))
            {
                entry.Touch();
                return entry.Token;
            }

            entry = _grainCancellationTokens.GetOrAdd(tokenId, _createGrainCancellationTokenEntry);
            if (isCancellationRequested)
            {
                entry.Token.Cancel();
            }

            return entry.Token;
        }

        private CancellationTokenSource RecordCancellationToken(Guid tokenId, bool isCancellationRequested)
        {
            _cancellationTokens ??= new ConcurrentDictionary<Guid, Entry<CancellationTokenSource>>();

            if (_cancellationTokens.TryGetValue(tokenId, out var entry))
            {
                entry.Touch();
                return entry.Token;
            }

            entry = _cancellationTokens.GetOrAdd(tokenId, _createCancellationTokenEntry);
            if (isCancellationRequested)
            {
                entry.Token.Cancel();
            }

            return entry.Token;
        }

        private void ExpireTokens()
        {
            var now = Stopwatch.GetTimestamp();
            if (_grainCancellationTokens is not null)
            {
                foreach (var token in _grainCancellationTokens)
                {
                    if (token.Value.IsExpired(_cleanupFrequency, now))
                    {
                        _grainCancellationTokens.TryRemove(token.Key, out _);
                    }
                }
            }

            if (_cancellationTokens is not null)
            {
                foreach (var token in _cancellationTokens)
                {
                    if (token.Value.IsExpired(_cleanupFrequency, now))
                    {
                        _grainCancellationTokens.TryRemove(token.Key, out _);
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cleanupTimer.Dispose();
        }

        private class Entry<TCancellationToken>
        {
            private long _createdTime;

            public Entry(TCancellationToken token)
            {
                Token = token;
                _createdTime = Stopwatch.GetTimestamp();
            }

            public void Touch() => _createdTime = Stopwatch.GetTimestamp();

            public TCancellationToken Token { get; }

            public bool IsExpired(TimeSpan expiry, long nowTimestamp)
            {
                var untouchedTime = TimeSpan.FromSeconds((nowTimestamp - _createdTime) / (double)Stopwatch.Frequency);

                return untouchedTime >= expiry;
            }
        }
    }
}