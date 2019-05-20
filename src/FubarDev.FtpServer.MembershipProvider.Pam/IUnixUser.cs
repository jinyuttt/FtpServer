// <copyright file="IUnixUser.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using FubarDev.FtpServer.AccountManagement;

namespace FubarDev.FtpServer.MembershipProvider.Pam
{
    /// <summary>
    /// Interface for unix user specific information.
    /// </summary>
    public interface IUnixUser : IFtpUser
    {
        /// <summary>
        /// Gets the user identifier.
        /// </summary>
        long UserId { get; }

        /// <summary>
        /// Gets the group identifier.
        /// </summary>
        long GroupId { get; }
    }
}
