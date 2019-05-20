// <copyright file="FsIdChanger.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using FubarDev.FtpServer.Commands;
using FubarDev.FtpServer.Features;
using FubarDev.FtpServer.FileSystem.Unix;
using FubarDev.FtpServer.MembershipProvider.Pam;

using Microsoft.Extensions.Logging;

namespace TestFtpServer.CommandMiddlewares
{
    /// <summary>
    /// Change the user and group IDs for file system operations.
    /// </summary>
    public class FsIdChanger : IFtpCommandMiddleware
    {
        private readonly ILogger<FsIdChanger>? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FsIdChanger"/> class.
        /// </summary>
        /// <param name="logger"></param>
        public FsIdChanger(
            ILogger<FsIdChanger>? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public Task InvokeAsync(FtpExecutionContext context, FtpCommandExecutionDelegate next)
        {
            var authInfo = context.Connection.Features.Get<IAuthorizationInformationFeature>();
            if (!(authInfo.User is IUnixUser unixUser))
            {
                return next(context);
            }

            var fsInfo = context.Connection.Features.Get<IFileSystemFeature>();
            if (!(fsInfo.FileSystem is UnixFileSystem))
            {
                return next(context);
            }

            return ExecuteWithChangedFsId(context, unixUser, next);
        }

        private async Task ExecuteWithChangedFsId(
            FtpExecutionContext context,
            IUnixUser unixUser,
            FtpCommandExecutionDelegate next)
        {
            using var _ = new UnixFileSystemIdChanger(_logger, unixUser.UserId, unixUser.GroupId);
            await next(context).ConfigureAwait(true);
        }

        /// <summary>
        /// Class to temporarily change the file system user and group identifiers.
        /// </summary>
        private class UnixFileSystemIdChanger : IDisposable
        {
            private readonly bool _hasUserInfo;
            private readonly uint _oldUserId;
            private readonly uint _oldGroupId;

            /// <summary>
            /// Initializes a new instance of the <see cref="UnixFileSystemIdChanger"/> class.
            /// </summary>
            /// <param name="logger">The logger.</param>
            /// <param name="userId">The user identifier.</param>
            /// <param name="groupId">The group identifier.</param>
            public UnixFileSystemIdChanger(
                ILogger? logger,
                long userId,
                long groupId)
            {
                _hasUserInfo = true;
                _oldGroupId = ChangeGroupId((uint)groupId);
                try
                {
                    _oldUserId = ChangeUserId((uint)userId);
                }
                catch
                {
                    UnixInterop.setfsgid(_oldGroupId);
                    throw;
                }

                logger?.LogTrace("Switched to user id={userId} (was: {oldUserId}) and group id={groupId} (was: {oldGroupId})", userId, _oldUserId, groupId, _oldGroupId);
            }

            /// <inheritdoc />
            public void Dispose()
            {
                if (_hasUserInfo)
                {
                    UnixInterop.setfsgid(_oldGroupId);
                    UnixInterop.setfsuid(_oldUserId);
                }
            }

            private static uint ChangeUserId(uint userId)
            {
                var oldId = UnixInterop.setfsuid(userId);
                if (oldId == userId)
                {
                    return oldId;
                }

                // This will always fail and is required, because no
                // error status gets set by this function.
                var currentId = UnixInterop.setfsuid(uint.MaxValue);
                if (currentId != userId)
                {
                    if (currentId != oldId)
                    {
                        UnixInterop.setfsuid(oldId);
                    }

                    throw new InvalidOperationException();
                }

                // Set again, because WSL seems to be buggy and accepts
                // uint.MaxValue even though it's not a valid user id.
                UnixInterop.setfsuid(userId);

                return oldId;
            }

            private static uint ChangeGroupId(uint groupId)
            {
                var oldId = UnixInterop.setfsgid(groupId);
                if (oldId == groupId)
                {
                    return oldId;
                }

                // This will always fail and is required, because no
                // error status gets set by this function.
                var currentId = UnixInterop.setfsgid(uint.MaxValue);
                if (currentId != groupId)
                {
                    if (currentId != oldId)
                    {
                        UnixInterop.setfsgid(oldId);
                    }

                    throw new InvalidOperationException();
                }

                // Set again, because WSL seems to be buggy and accepts
                // uint.MaxValue even though it's not a valid group id.
                UnixInterop.setfsgid(groupId);

                return oldId;
            }
        }

        /// <summary>
        /// Interop functions.
        /// </summary>
        // ReSharper disable IdentifierTypo
        // ReSharper disable StringLiteralTypo
        private static class UnixInterop
        {
            /// <summary>
            /// Set user identity used for filesystem checks.
            /// </summary>
            /// <param name="fsuid">The user identifier.</param>
            /// <returns>Previous user identifier.</returns>
            [DllImport("libc.so.6", SetLastError = true)]
            [SuppressMessage("ReSharper", "SA1300", Justification = "It's a C function.")]
#pragma warning disable IDE1006 // Benennungsstile
            public static extern uint setfsuid(uint fsuid);
#pragma warning restore IDE1006 // Benennungsstile

            /// <summary>
            /// Set group identity used for filesystem checks.
            /// </summary>
            /// <param name="fsgid">The group identifier.</param>
            /// <returns>Previous group identifier.</returns>
            [DllImport("libc.so.6", SetLastError = true)]
            [SuppressMessage("ReSharper", "SA1300", Justification = "It's a C function.")]
#pragma warning disable IDE1006 // Benennungsstile
            public static extern uint setfsgid(uint fsgid);
#pragma warning restore IDE1006 // Benennungsstile
        }
        // ReSharper restore StringLiteralTypo
        // ReSharper restore IdentifierTypo
    }
}
