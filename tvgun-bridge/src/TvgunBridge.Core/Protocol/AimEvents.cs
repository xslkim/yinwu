namespace TvgunBridge.Core.Protocol;

/// <summary>
/// A single aim sample reported by the phone, in 1920x1080 normalized coordinates.
/// </summary>
/// <param name="X">Normalized X coordinate (0 = left outer edge of the white frame).</param>
/// <param name="Y">Normalized Y coordinate (0 = top outer edge of the white frame).</param>
/// <param name="Timestamp">Local time at which the sample was received.</param>
public readonly record struct AimPoint(double X, double Y, DateTimeOffset Timestamp);

/// <summary>
/// A trigger pull reported by the phone via <c>POST /shot</c>. The tvgun protocol only
/// sends press events; there is no release event.
/// </summary>
/// <param name="X">Normalized X coordinate at the moment of the shot.</param>
/// <param name="Y">Normalized Y coordinate at the moment of the shot.</param>
/// <param name="Timestamp">Local time at which the shot was received.</param>
public readonly record struct ShotEvent(double X, double Y, DateTimeOffset Timestamp);
