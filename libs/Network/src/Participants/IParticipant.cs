// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Sharing
{
    /// <summary>
    /// Represents an identified participant.
    /// </summary>
    public interface IParticipant
    {
        /// <summary>
        /// Gets the id of the participant.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the display name of the particpant.
        /// </summary>
        string DisplayName { get; }
    }
}
