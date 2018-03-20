// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Linq;
using CivOne.Graphics;
using CivOne.Tiles;
using CivOne.Units;
using CivOne.UserInterface;
using CivOne.Buildings;
using CivOne.Tasks;
using System.Collections.Generic;

namespace CivOne.Screens.Dialogs
{
	internal class DiplomatSabotage : BaseDialog
	{
		private const int FONT_ID = 0;

		private readonly City _enemyCity;
		private readonly Diplomat _diplomat;

		private string Sabotage()
		{
			Game.DisbandUnit(_diplomat);

			IList<IBuilding> buildings = _enemyCity.Buildings.Where(b => (b.GetType() != typeof(Buildings.Palace))).ToList();

			int random = Common.Random.Next(0, buildings.Count);

			if (random == buildings.Count)
			{
				_enemyCity.Shields = (ushort)0;
				string production = (_enemyCity.CurrentProduction as ICivilopedia).Name;
				return $"{production} production sabotaged";
			}
			else
			{
				// sabotage a building
				_enemyCity.RemoveBuilding(buildings[random]);
				return $"{buildings[random].Name} sabotaged";
			}
		}

		internal DiplomatSabotage(City enemyCity, Diplomat diplomat) : base(60, 80, 220, 56)
		{
			_enemyCity = enemyCity ?? throw new ArgumentNullException(nameof(enemyCity));
			_diplomat = diplomat ?? throw new ArgumentNullException(nameof(diplomat));

			IBitmap spyPortrait = Icons.Spy;

			Palette palette = Common.DefaultPalette;
			for (int i = 144; i < 256; i++)
			{
				palette[i] = spyPortrait.Palette[i];
			}
			this.SetPalette(palette);

			DialogBox.AddLayer(spyPortrait, 2, 2);

			DialogBox.DrawText($"Spies Report", 0, 15, 45, 5);
			DialogBox.DrawText(Sabotage(), 0, 15, 45, 5 + Resources.GetFontHeight(FONT_ID));
			DialogBox.DrawText($"in {_enemyCity.Name}", 0, 15, 45, 5 + (2 * Resources.GetFontHeight(FONT_ID)));
		}
	}
}