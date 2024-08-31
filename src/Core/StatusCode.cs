// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Core;

public enum StatusCode : byte
{
    /// <summary>
    /// Request was successfully processed and a response was returned.
    /// </summary>
    Ok = 0,
    /// <summary>
    /// An error occured while processing the request.
    /// The response contains a string describing the error.
    /// </summary>
    Error = 1
}
