// Draw order of everything in the world, in one place. Tiles use -5..2 and the background -12..-9 (see Tile.cs and Background.cs).
public static class Layers
{
    public const int LinkOutline = 3, LinkTrack = 4, LinkFlow = 5, LinkPort = 6;
    public const int TileGhost = 7;
    public const int NodeBody = 8, NodeBand = 9, NodeSlider = 10, NodeOutline = 11, NodeBurst = 12;
    public const int Clasp = 13;          // joins two snapped buildings, above their outlines
    public const int NodeIcon = 14, NodeText = 15;
    public const int Items = 17;          // things travelling along edges, above the buildings
    public const int Ghost = 19;
    public const int Mist = 21;
    public const int FloatText = 22;
}
