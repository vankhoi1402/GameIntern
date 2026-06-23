/// <summary>
/// Enum định danh loại tile Mahjong — dùng để kiểm tra merge (phải cùng loại mới gộp được).
/// </summary>
public enum BlockType
{
    None = 0,

    // --- BỘ VĂN (DOT) ---
    Dot_1 = 1, Dot_2 = 2, Dot_3 = 3, Dot_4 = 4, Dot_5 = 5,

    // --- BỘ SÁCH (BAMBOO) ---
    Bamboo_1 = 6, Bamboo_2 = 7, Bamboo_3 = 8, Bamboo_4 = 9, Bamboo_5 = 10,

    // --- BỘ VẠN (CHARACTER) ---
    Char_1 = 11, Char_2 = 12, Char_3 = 13, Char_4 = 14, Char_5 = 15,

    // --- BỘ GIÓ VÀ RỒNG (WIND & DRAGON) ---
    Wind_East = 16,
    Wind_West = 17,
    Dragon_Red = 18,
    Dragon_Green = 19,
    Dragon_White = 20
}
