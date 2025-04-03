namespace K4RPG
{
	using CounterStrikeSharp.API.Core;
	using K4RPG.Models;
    using Microsoft.Extensions.Logging;

	public sealed partial class Plugin : BasePlugin
	{
		public void Initialize_Events()
		{
			RegisterEventHandler((EventPlayerActivate @event, GameEventInfo info) =>
			{
				CCSPlayerController? playerController = @event.Userid;

				if (playerController?.IsValid == true)
				{
					if (playerController.IsHLTV || playerController.IsBot)
						return HookResult.Continue;

					RPGPlayer newPlayer = new RPGPlayer(this, playerController);
					RPGPlayers.Add(newPlayer);

					Task.Run(() => newPlayer.LoadPlayerDataAsync());
				}
				return HookResult.Continue;
			});

			RegisterEventHandler((EventPlayerSpawn @event, GameEventInfo info) =>
            {
                RPGPlayer? cPlayer = GetPlayer(@event.Userid);

                if (cPlayer is null || !cPlayer.IsValid)
                    return HookResult.Continue;

                // Create a snapshot of skills
                var skillsSnapshot = cPlayer.Skills.ToArray();

                foreach (var (skillId, level) in skillsSnapshot)
                {
                    if (RPGSkills.FirstOrDefault(y => y.ID == skillId) is RPGSkill skill)
                    {
                        try
                        {
                            skill.Apply(cPlayer.Controller, level);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Error applying skill {skillId}: {ex.Message}");
                        }
                    }
                }

                return HookResult.Continue;
            }, HookMode.Post);

            RegisterEventHandler((EventPlayerDisconnect @event, GameEventInfo info) =>
			{
				RPGPlayer? cPlayer = GetPlayer(@event.Userid);

				if (cPlayer is null)
					return HookResult.Continue;

				Task.Run(async () =>
				{
					await cPlayer.SavePlayerDataAsync();
					RPGPlayers.Remove(cPlayer);
				});

				return HookResult.Continue;
			});

			RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
			{
				foreach (RPGPlayer cPlayer in RPGPlayers)
				{
					if (cPlayer.IsValid && cPlayer.RoundExperience > 0)
					{
						cPlayer.Controller.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.experience.earnt", cPlayer.RoundExperience]}");
						cPlayer.RoundExperience = 0;
					}
				}

                Task.Run(async () =>
                {
                    await SaveAllPlayersDataAsync();
                    await CleanDuplicateSkillsAsync();
                });

                return HookResult.Continue;
			});
		}
	}
}