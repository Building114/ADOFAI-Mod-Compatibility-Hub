using Overlayer.Tags.Attributes;
using UnityEngine;

namespace Overlayer.Tags;

/// <summary>Player and planet indexes are both 1-based.</summary>
public static class PlanetColors {
    private const string MissingColor = "808080FF";

    [Tag(NotPlaying = true)]
    [TagDesc("获取指定玩家的指定球体颜色，玩家和球体编号均从1开始，返回RRGGBBAA。不存在时返回808080FF。\n示例：{PlayerPlanetColor(1,1)}；灰色渐变：<color=#{ColorPulse(808080,{PlayerPlanetColor(1,1)},1200)}>Text</color>")]
    public static string PlayerPlanetColor(int player = 1, int planet = 1) {
        // ADOFAI 3.4.0: scrPlayerManager.players -> PlanetarySystem.planetList
        // -> scrPlanet.planetRenderer.planetColor.ToRealColor().
        if(player < 1 || player > scrPlayerManager.MaxPlayers || planet < 1) return MissingColor;
        // Do not repeatedly search the scene for a manager while in the menu.
        if(scrController.instance == null) return MissingColor;
        scrPlayerManager manager = scrPlayerManager.instance;
        scrPlayer[] players = manager?.players;
        if(players == null || player > players.Length) return MissingColor;

        scrPlayer selectedPlayer = players[player - 1];
        if(selectedPlayer == null || selectedPlayer.planetarySystem == null) return MissingColor;
        var planets = selectedPlayer.planetarySystem.planetList;
        if(planets == null || planet > planets.Count) return MissingColor;

        scrPlanet selectedPlanet = planets[planet - 1];
        if(selectedPlanet == null || selectedPlanet.planetRenderer == null) return MissingColor;
        return ColorUtility.ToHtmlStringRGBA(selectedPlanet.planetRenderer.planetColor.ToRealColor());
    }
}
