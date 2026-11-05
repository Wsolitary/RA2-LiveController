namespace RA2LiveController.Core
{
    public static class GameConstants
    {
        // ==========================================
        // YR 1.001 / Ares Global Memory Arrays
        // ==========================================
        public const uint ADDR_HOUSE_ITEMS    = 0x00A8022C;
        public const uint ADDR_HOUSE_COUNT    = 0x00A80238;
        public const uint ADDR_CURRENT_PLAYER = 0x00A83D4C;
        public const uint ADDR_FACTORY_ITEMS  = 0x00A83E34;
        public const uint ADDR_FACTORY_COUNT  = 0x00A83E40;
        public const uint ADDR_FOOT_ITEMS     = 0x008B3DC4;
        public const uint ADDR_FOOT_COUNT     = 0x008B3DD0;
        public const uint ADDR_AIRCRAFT_ITEMS = 0x00A8E394;
        public const uint ADDR_AIRCRAFT_COUNT = 0x00A8E3A0;
        public const uint ADDR_BUILDING_ITEMS = 0x00A8EB44;
        public const uint ADDR_BUILDING_COUNT = 0x00A8EB50;
        public const uint ADDR_MAP_INSTANCE   = 0x0087F7E8;

        // ==========================================
        // Westwood Engine Function Pointers
        // ==========================================
        public const uint FN_MAP_REVEAL_ALL   = 0x00577D90; // MapClass::Reveal_All(HouseClass*)
        public const uint FN_HOUSE_DESTROY_ALL= 0x004FC6D0; // HouseClass::DestroyAll()
        public const uint FN_HOUSE_ACCEPT_DEF = 0x004FC0B0; // HouseClass::AcceptDefeat()

        // ==========================================
        // TechnoClass & HouseClass Offsets
        // ==========================================
        public const uint OFFSET_HOUSE_CASH           = 0x30C;
        public const uint OFFSET_HOUSE_ORE_TIB0       = 0x2FC;
        public const uint OFFSET_HOUSE_BLDG_ITEMS     = 0x6C;
        public const uint OFFSET_HOUSE_BLDG_COUNT     = 0x78;
        public const uint OFFSET_HOUSE_IS_PLAYER      = 0x1ED;
        public const uint OFFSET_HOUSE_MAP_CLEAR      = 0x241;
        public const uint OFFSET_HOUSE_SPYSAT         = 0x577A;
        public const uint OFFSET_HOUSE_FIREPOWER_MULT = 0x188;
        public const uint OFFSET_HOUSE_GROUND_SPEED   = 0x190;
        public const uint OFFSET_HOUSE_AIR_SPEED      = 0x198;
        public const uint OFFSET_HOUSE_ROF_MULT       = 0x1A8;
        public const uint OFFSET_HOUSE_BUILD_SPEED    = 0x1B8;
        public const uint OFFSET_HOUSE_INI_NAME       = 0x16009;
        public const uint OFFSET_HOUSE_UI_NAME        = 0x1602A;

        public const uint OFFSET_TECHNO_HEALTH        = 0x6C;
        public const uint OFFSET_TECHNO_MISSION       = 0xAC;
        public const uint OFFSET_TECHNO_TIMER         = 0xC0;
        public const uint OFFSET_TECHNO_DEACTIVATED   = 0x1C8;
        public const uint OFFSET_TECHNO_OWNER         = 0x21C;
        public const uint OFFSET_TECHNO_RELOAD_TIMER  = 0x2C4;
        public const uint OFFSET_TECHNO_SPEED_PCT     = 0x578;
        public const uint OFFSET_TECHNO_SPEED_MULT    = 0x580;
    }
}
