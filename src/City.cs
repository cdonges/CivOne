// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Interfaces;
using CivOne.Screens;
using CivOne.Tasks;
using CivOne.Units;
using CivOne.Wonders;

namespace CivOne
{
	public class City : ITurn
	{
		private Map Map
		{
			get
			{
				return Map.Instance;
			}
		}

		internal byte X;
		internal byte Y;
		private byte _owner;
		internal byte Owner
		{
			get
			{
				return _owner;
			}
			set
			{
				_owner = value;
				ResetResourceTiles();
			}
		}
		internal string Name;
		private byte _size;
		internal byte Size
		{
			get
			{
				return _size;
			}
			set
			{
				_size = value;
				if (_size == 0)
				{
					Game.Instance.DestroyCity(this);
					Map[X, Y].Road = false;
					Map[X, Y].Irrigation = false;
					return;
				}
				SetResourceTiles();
			}
		}
		internal int Shields { get; private set; }
		internal int Food { get; private set; }
		internal IProduction CurrentProduction { get; private set; }
		private List<ITile> _resourceTiles = new List<ITile>();
		private List<IBuilding> _buildings = new List<IBuilding>();
		private List<IWonder> _wonders = new List<IWonder>();

		public IBuilding[] Buildings
		{
			get
			{
				return _buildings.OrderBy(b => b.Id).ToArray();
			}
		}

		public IWonder[] Wonders
		{
			get
			{
				return _wonders.OrderBy(b => b.Id).ToArray();
			}
		}

		public bool HasBuilding(IBuilding building)
		{
			return _buildings.Any(b => b.Id == building.Id);
		}

		public bool HasBuilding<T>() where T : IBuilding
		{
			return _buildings.Any(b => b is T);
		}

		public bool HasWonder(IWonder wonder)
		{
			return _wonders.Any(w => w.Id == wonder.Id);
		}

		public bool HasWonder<T>() where T : IWonder
		{
			return _wonders.Any(w => w is T);
		}

		internal int ShieldCosts
		{
			get
			{
				switch (Game.Instance.GetPlayer(_owner).Government)
				{
					case Government.Anarchy:
					case Government.Despotism:
						int costs = 0;
						for (int i = 0; i < Units.Count(u => (!(u is Diplomat) || (u is Caravan))); i++)
						{
							if (i < _size) continue;
							costs++;
						}
						return costs;
					default:
						return Units.Length;
				} 
			}
		}

		internal int ShieldIncome
		{
			get
			{
				return ShieldTotal - ShieldCosts;
			}
		}
		
		internal int FoodCosts
		{
			get
			{
				int costs = (_size * 2);
				switch (Game.Instance.GetPlayer(_owner).Government)
				{
					case Government.Anarchy:
					case Government.Despotism:
						costs += Units.Count(u => (u is Settlers));
						break;
					default:
						costs += (Units.Count(u => (u is Settlers)) * 2);
						break;
				} 
				return costs;
			}
		}

		internal int FoodIncome
		{
			get
			{
				return ResourceTiles.Sum(t => t.Food) - FoodCosts;
			}
		}

		internal int FoodTotal
		{
			get
			{
				return ResourceTiles.Sum(t => t.Food);
			}
		}

		internal int FoodRequired
		{
			get
			{
				return (int)(Size + 1) * 10;
			}
		}

		internal int ShieldTotal
		{
			get
			{
				int shields = ResourceTiles.Sum(t => t.Shield);
				if (_buildings.Any(b => (b is Factory))) shields += (short)Math.Floor((double)shields * (_buildings.Any(b => (b is NuclearPlant)) ? 1.0 : 0.5));
				if (_buildings.Any(b => (b is MfgPlant))) shields += (short)Math.Floor((double)shields * 1.0);
				return shields;
			}
		}

		internal int TradeTotal
		{
			get
			{
				return ResourceTiles.Sum(t => t.Trade);
			}
		}

		internal short Luxuries
		{
			get
			{
				short luxuries = (short)Math.Round(((double)(TradeTotal - Science) / (10 - Player.ScienceRate)) * Player.LuxuriesRate);
				if (_buildings.Any(b => (b is Bank))) luxuries += (short)Math.Floor((double)luxuries * 0.5);
				return luxuries;
			}
		}

		internal short Taxes
		{
			get
			{
				short taxes = (short)(TradeTotal - Luxuries - Science);
				if (_buildings.Any(b => (b is MarketPlace))) taxes += (short)Math.Floor((double)taxes * 0.5);
				if (_buildings.Any(b => (b is Bank))) taxes += (short)Math.Floor((double)taxes * 0.5);
				return taxes;
			}
		}

		internal short Science
		{
			get
			{
				short science = (short)Math.Round(((double)TradeTotal / 10) * Player.ScienceRate);
				if (HasBuilding<Library>()) science += (short)Math.Floor((double)science * 0.5);
				if (HasBuilding<University>()) science += (short)Math.Floor((double)science * 0.5);
				if (!Player.WonderObsolete<CopernicusObservatory>() && HasWonder<CopernicusObservatory>()) science += (short)Math.Floor((double)science * 1.0);
				if (Player.HasWonder<SETIProgram>()) science += (short)Math.Floor((double)science * 0.5);
				return science;
			}
		}

		internal short TotalMaintenance
		{
			get
			{
				return (short)_buildings.Sum(b => b.Maintenance);
			}
		}

		internal IEnumerable<ITile> ResourceTiles
		{
			get
			{
				return CityTiles.Where(t => (t.X == X && t.Y == Y) || _resourceTiles.Contains(t));
			}
		}

		internal bool OccupiedTile(ITile tile)
		{
			if (ResourceTiles.Any(t => t.X == tile.X && t.Y == tile.Y))
				return false;
			return (tile.City != null || Game.Instance.GetCities().Any(c => c.ResourceTiles.Any(t => t.X == tile.X && t.Y == tile.Y)));
		}

		private void SetResourceTiles()
		{
			while (_resourceTiles.Count > Size)
				_resourceTiles.RemoveAt(_resourceTiles.Count - 1);
			if (_resourceTiles.Count == Size) return;
			if (_resourceTiles.Count < Size)
			{
				IEnumerable<ITile> tiles = CityTiles.Where(t => !OccupiedTile(t) && !ResourceTiles.Contains(t)).OrderByDescending(t => t.Food).ThenByDescending(t => t.Shield).ThenByDescending(t => t.Trade);
				if (tiles.Count() > 0)
					_resourceTiles.Add(tiles.First());
			}
		}

		private void ResetResourceTiles()
		{
			_resourceTiles.Clear();
			for (int i = 0; i < Size; i++)
				SetResourceTiles();
		}

		public void SetResourceTile(ITile tile)
		{
			if (tile == null || OccupiedTile(tile) || !CityTiles.Contains(tile) || (tile.X == X && tile.Y == Y) || (_resourceTiles.Count >= Size && !_resourceTiles.Contains(tile)))
			{
				ResetResourceTiles();
				return;
			}
			if (_resourceTiles.Contains(tile))
			{
				_resourceTiles.Remove(tile);
				return;
			}
			_resourceTiles.Add(tile);
		}

		private Player Player
		{
			get
			{
				return Game.Instance.GetPlayer(Owner);
			}
		}

		public IEnumerable<IProduction> AvailableProduction
		{
			get
			{
				foreach (IUnit unit in Reflect.GetUnits().Where(u => Player.ProductionAvailable(u)))
				{
					if (unit.Class == UnitClass.Water && !Map[X, Y].GetBorderTiles().Any(t => t.IsOcean)) continue;
					yield return unit;
				}
				foreach (IBuilding building in Reflect.GetBuildings().Where(b => Player.ProductionAvailable(b) && !_buildings.Any(x => x.Id == b.Id)))
				{
					yield return building;
				}
				foreach (IWonder wonder in Reflect.GetWonders().Where(b => Player.ProductionAvailable(b)))
				{
					yield return wonder;
				}
			}
		}

		public void SetProduction(IProduction production)
		{
			CurrentProduction = production;
		}

		public void Buy()
		{
			// DEBUG CODE
			Shields = (int)CurrentProduction.Price * 10;
		}

		public int Population
		{
			get
			{
				int output = 0;
				for (int i = 1; i <= Size; i++)
				{
					output += 10000 * i;
				}
				return output;
			}
		}

		internal IEnumerable<Citizen> Citizens
		{
			get
			{
				for (int i = 0; i < Size; i++)
				{
					if (i < ResourceTiles.Count() - 1)
					{
						yield return (i % 2 == 0) ? Citizen.ContentMale : Citizen.ContentFemale;
						continue;
					}
					yield return Citizen.Entertainer;
				}
			}
		}

		private IEnumerable<ITile> CityTiles
		{
			get
			{
				ITile[,] tiles = CityRadius;
				for (int xx = 0; xx < 5; xx++)
				for (int yy = 0; yy < 5; yy++)
				{
					if (tiles[xx, yy] == null) continue;
					yield return tiles[xx, yy];
				}
			}
		}

		public ITile[,] CityRadius
		{
			get
			{
				Player player = Game.Instance.GetPlayer(Owner);
				ITile[,] tiles = Map[X - 2, Y - 2, 5, 5];
				for (int xx = 0; xx < 5; xx++)
				for (int yy = 0; yy < 5; yy++)
				{
					ITile tile = tiles[xx, yy];
					if (tile == null) continue;
					if ((xx == 0 || xx == 4) && (yy == 0 || yy == 4)) tiles[xx, yy] = null;
					if (!player.Visible(tile)) tiles[xx, yy] = null;
				}
				return tiles;
			}
		}

		public IUnit[] Units
		{
			get
			{
				return Game.Instance.GetUnits().Where(u => u.Home == this).ToArray();
			}
		}

		public ITile Tile
		{
			get
			{
				return Map[X, Y];
			}
		}

		public void AddBuilding(IBuilding building)
		{
			_buildings.Add(building);
		}

		public void RemoveBuilding(IBuilding building)
		{
			_buildings.RemoveAll(b => b.Id == building.Id);
		}

		public void NewTurn()
		{
			Food += FoodIncome;
			if (Food < 0)
			{
				Food = 0;
				Size--;
				if (Owner == Game.Instance.PlayerNumber(Game.Instance.HumanPlayer))
				{
					GameTask.Enqueue(Message.Newspaper(this, "Food storage exhausted", $"in {Name}!", "Famine feared."));
				}
				if (Size == 0) return;
			}
			else if (Food > FoodRequired)
			{
				Food -= FoodRequired;

				if (Size == 10 && !_buildings.Any(b => b.Id == (int)Building.Aqueduct))
				{
					GameTask.Enqueue(Message.Advisor(Advisor.Domestic, $"{Name} requires an AQUADUCT", "for further growth."));
				}
				else
				{
					Size++;
				}

				if (_buildings.Any(b => (b is Granary)))
				{
					if (Food < (FoodRequired / 2))
					{
						Food = (FoodRequired / 2);
					}
				}
			}

			if (ShieldIncome > 0)
				Shields += ShieldIncome;
			if (Shields >= (int)CurrentProduction.Price * 10)
			{
				if (CurrentProduction is IUnit)
				{
					if (CurrentProduction is Settlers)
					{
						//TODO: This should do something to the food supply
						if (Size == 1) Size++;
						Size--;
					}
					Shields = 0;
					IUnit unit = Game.Instance.CreateUnit((CurrentProduction as IUnit).Type, X, Y, Owner);
					unit.SetHome(this);
					unit.Veteran = (_buildings.Any(b => (b is Barracks)));
					if ((unit is Settlers) || (unit is Diplomat) || (unit is Caravan))
					{
						GameTask advisorMessage = Message.Advisor(Advisor.Defense, $"{this.Name} builds {unit.Name}.");
						advisorMessage.Done += (s, a) => GameTask.Insert(Show.CityManager(this));//Common.AddScreen(new CityManager(this));
						GameTask.Enqueue(advisorMessage);
					}
				}
				if (CurrentProduction is IBuilding && !_buildings.Any(b => b.Id == (CurrentProduction as IBuilding).Id))
				{
					Shields = 0;
					if (CurrentProduction is ISpaceShip)
					{
						Message message = Message.Newspaper(this, $"{this.Name} builds", $"{(CurrentProduction as ICivilopedia).Name}.");
						message.Done += (s, a) => {
							// TODO: Add space ship component
							GameTask.Insert(Show.CityManager(this));
						};
						GameTask.Enqueue(message);
					}
					else if (CurrentProduction is Palace)
					{
						foreach (City city in Game.Instance.GetCities().Where(c => c.Owner == Owner))
						{
							// Remove palace from all buildings.
							city.RemoveBuilding(CurrentProduction as Palace);
						}
						_buildings.Add(CurrentProduction as IBuilding);
						
						Message message = Message.Newspaper(this, $"{this.Name} builds", $"{(CurrentProduction as ICivilopedia).Name}.");
						message.Done += (s, a) => {
							GameTask advisorMessage = Message.Advisor(Advisor.Foreign, $"{Player.TribeName} capital", $"moved to {Name}.");
							advisorMessage.Done += (s1, a1) => GameTask.Insert(Show.CityManager(this)); //Common.AddScreen(new CityManager(this));
							GameTask.Enqueue(advisorMessage);
						};
						GameTask.Enqueue(message);
					}
					else
					{
						_buildings.Add(CurrentProduction as IBuilding);
						GameTask.Enqueue(new ImprovementBuilt(this, (CurrentProduction as IBuilding)));
					}
				}
				if (CurrentProduction is IWonder && !Game.Instance.BuiltWonders.Any(w => w.Id == (CurrentProduction as IWonder).Id))
				{
					Shields = 0;
					_wonders.Add(CurrentProduction as IWonder);
					GameTask.Enqueue(new ImprovementBuilt(this, (CurrentProduction as IWonder)));
				}
			}

			// TODO: Handle luxuries
			Player.Gold += Taxes;
			Player.Gold -= TotalMaintenance;
			Player.Science += Science;
			GameTask.Enqueue(new ProcessScience(Player));
		}

		internal City()
		{
			CurrentProduction = new Militia();
			SetResourceTiles();
		}
	}
}