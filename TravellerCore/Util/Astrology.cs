namespace TravellerCore.Util;


    /* A sector map looks much like this:

      _____         _____         _____         _____       
     /  X G\       /  D G\       /  X G\       /     \       / 
    /   @   \_____/   @   \_____/   @   \_____/       \_____/ 
    \NAME   /  B G\NAME   /  B G\NAME   /     \       /     \ 
     \ 0101/*  @   \ 0301/   @   \ 0501/       \_____/       \ 
     /  D G\NAME   /  D G\NAME   /  X G\       /  C G\       / 
    /   @   \ ____/   O   \^____/   @   \_____/   @   \_____/ 
    \NAME   /  D G\NAME   /  A G\NAME   /  X G\NAME   /  E G\ 
     \ 0102/   @   \ 0302/*  @   \ 0502/   @   \^0702/   @   \ 
     /  B G\NAME   /     \NAME   /     \NAME   /     \NAME   / 
    /   @   \^0201/       \ 0401/       \ 0601/       \ 0801/ 
    \NAME   /     \       /     \       /  X G\       /  D G\ 
     \ 0103/       \_____/       \_____/   @   \_____/   O   \ 
     /  E G\       /     \       /  C G\NAME   /  C G\NAME   / 
    /   @   \_____/       \_____/   @   \ 0602/   @   \ 0802/ 
    \NAME   /     \       /  X G\NAME   /  D G\NAME   /  X G\ 
     \ 0104/       \_____/   @   \^0504/   @   \^0704/   @   \ 
     /     \       /  X G\NAME   /  A G\NAME   /  A G\NAME   / 
    /       \_____/   O   \ 0403/   @   \^0603/   @   \ 0803/ 
    \       /  C G\NAME   /     \NAME   /  X G\NAME   /     \ 
     \_____/   @   \ 0305/       \ 0505/   @   \ 0705/       \ 
     /  B G\NAME   /  X G\       /     \NAME   /     \       / 
    /   @   \^0204/   @   \_____/       \ 0604/       \_____/ 
    \NAME   /     \NAME   /  C G\       /  X G\       /     \ 
     \ 0106/       \ 0306/   @   \_____/   @   \_____/       \ 
     /     \       /  D G\NAME   /     \NAME   /     \       / 
    /       \_____/   O   \ 0405/       \ 0605/       \_____/ 
    \       /  X G\NAME   /     \       /  D G\       /     \ 
     \_____/   @   \^0307/       \_____/   @   \_____/       \ 
     /     \NAME   /  X G\       /  E G\NAME   /     \       / 
    /       \ 0206/   @   \_____/   @   \^0606/       \_____/ 
    \       /     \NAME   /  X G\NAME   /  D G\       /     \ 
     \_____/       \ 0308/   @   \ 0508/   @   \_____/       \ 
     /     \       /     \NAME   /     \NAME   /  E G\       / 
    /       \_____/       \ 0407/       \ 0607/   @   \_____/ 
    \       /  B G\       /  D G\       /     \NAME   /     \ 
     \_____/   @   \_____/   @   \_____/       \ 0709/       \ 
     /  X G\NAME   /  B G\NAME   /  B G\       /     \       / 
    /   O   \ 0208/   @   \^0408/   @   \_____/       \_____/ 
    \NAME   /  A G\NAME   /  E G\NAME   /  X G\       /  C G\ 
     \ 0110/   @   \ 0310/   @   \ 0510/   @   \_____/   @   \ 
           \NAME   /     \NAME   /     \NAME   /     \NAME   /
            \ 0209/       \ 0409/       \ 0609/       \ 0809/   
    */
public static class Astrology
{
    /* We need the following functions:
     * 
     * Find shortest path
     * Find shortest path with limitations
     * 
     * And probably more.
     */
}
