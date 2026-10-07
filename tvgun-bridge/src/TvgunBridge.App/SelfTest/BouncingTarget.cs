namespace TvgunBridge.App.SelfTest;

/// <summary>
/// Pure bouncing-target physics for the self-test ("duck hunt") window. The target
/// drifts at constant velocity and reflects off the play-area edges (the area inside
/// the white frame). No UI dependencies; positions are in the caller's units.
/// </summary>
public sealed class BouncingTarget
{
    /// <summary>Creates a target at a random position with a random 45-degree-ish heading.</summary>
    public BouncingTarget(double radius, double speed, Random random, double width, double height, double inset)
    {
        Radius = radius;
        Speed = speed;
        Relocate(random, width, height, inset);
        var angle = random.NextDouble() * Math.PI * 2.0;
        VelocityX = Math.Cos(angle) * speed;
        VelocityY = Math.Sin(angle) * speed;
    }

    /// <summary>Target radius (hit area).</summary>
    public double Radius { get; }

    /// <summary>Constant speed in units per second.</summary>
    public double Speed { get; }

    /// <summary>Center X.</summary>
    public double X { get; private set; }

    /// <summary>Center Y.</summary>
    public double Y { get; private set; }

    /// <summary>Horizontal velocity (units/s).</summary>
    public double VelocityX { get; private set; }

    /// <summary>Vertical velocity (units/s).</summary>
    public double VelocityY { get; private set; }

    /// <summary>
    /// Advances the target by <paramref name="dtSeconds"/>, bouncing off the rectangle
    /// [<paramref name="inset"/>, width - inset] x [inset, height - inset] (radius-aware).
    /// </summary>
    public void Advance(double dtSeconds, double width, double height, double inset)
    {
        var minX = inset + Radius;
        var maxX = Math.Max(minX, width - inset - Radius);
        var minY = inset + Radius;
        var maxY = Math.Max(minY, height - inset - Radius);

        var x = X + VelocityX * dtSeconds;
        var y = Y + VelocityY * dtSeconds;

        if (x < minX)
        {
            x = 2 * minX - x;
            VelocityX = Math.Abs(VelocityX);
        }
        else if (x > maxX)
        {
            x = 2 * maxX - x;
            VelocityX = -Math.Abs(VelocityX);
        }

        if (y < minY)
        {
            y = 2 * minY - y;
            VelocityY = Math.Abs(VelocityY);
        }
        else if (y > maxY)
        {
            y = 2 * maxY - y;
            VelocityY = -Math.Abs(VelocityY);
        }

        X = Math.Clamp(x, minX, maxX);
        Y = Math.Clamp(y, minY, maxY);
    }

    /// <summary>Moves the target to a new random position inside the play area.</summary>
    public void Relocate(Random random, double width, double height, double inset)
    {
        var minX = inset + Radius;
        var maxX = Math.Max(minX, width - inset - Radius);
        var minY = inset + Radius;
        var maxY = Math.Max(minY, height - inset - Radius);
        X = minX + random.NextDouble() * (maxX - minX);
        Y = minY + random.NextDouble() * (maxY - minY);
    }

    /// <summary>True when the point lies inside the target's hit circle.</summary>
    public bool IsHit(double px, double py)
    {
        var dx = px - X;
        var dy = py - Y;
        return Math.Sqrt(dx * dx + dy * dy) < Radius;
    }
}
