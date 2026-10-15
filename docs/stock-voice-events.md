# Stock voice events (Sea Power v0.8.6, `voice.ini` [US] + call sites)

Generated from `StreamingAssets/original/audio/voiceover/voice.ini` and the decompiled call sites of
`VoiceMessage.PlayVoiceMessage / PlayVoiceSequence / PlayAircraftAltitude / AddVoiceToSequence`.
Priority: 1 abandon · 2 hit/bingo/incoming · 3 hit target · 4 firing · 5 contacts · 6 nav/systems · 7 confirmations (lower = more urgent; ≤2 = urgent).
Numbers: `DIGITS` digit-by-digit · `DIGITS_AIRCRAFT` / `DIGITS_EXCITED` alt digit sets · `FULL` spoken number · `DesignateTrack` = chained ending clip.

Keys with no literal call site are either built dynamically (see notes) or unused.

| Group | Key | Prio | Numbers | Ending | Variants | Call sites |
|---|---|---|---|---|---|---|
| 1A - Order Confirmations | `Confirmed` | 7 |  |  | 1 | — |
| 1A - Order Confirmations | `RogerThat` | 7 |  |  | 1 | `Seapower/RefuelTask.cs:65`, `SeapowerUI.ViewModels/RebaseState.cs:43`, `SeapowerUI.ViewModels/RebaseState.cs:61` |
| 1A - Order Confirmations | `YesCommander` | 7 |  |  | 1 | — |
| 1A - Order Confirmations | `Yes` | 7 |  |  | 1 | — |
| 1B - Order Denials | `Negative` | 7 |  |  | 1 | `Seapower/ObjectBase.cs:10043` |
| 1B - Order Denials | `UnableToComply` | 7 |  |  | 1 | `Seapower/FlightDeck.cs:2214`, `Seapower/HandlePendingLaunchTask.cs:48`, `Seapower/ObjectBase.cs:10013`, `Seapower/ObjectBase.cs:10028` …(+17) |
| 1C - Formations | `ReturningToFormation` | 6 |  |  | 2 | `Seapower/UnitFormation.cs:608` |
| 1C - Formations | `ReturningToFormationAir` | 6 |  |  | 1 | — |
| 1D - Manuevers (Ownship only except for turn order confirmation ) | `TurnToCourse` | 6 | DIGITS | HelmAye | 1 | `SeapowerUI.ViewModels/UnitSelectedState.cs:457` |
| 1D - Manuevers (Ownship only except for turn order confirmation ) | `TurnToCourse_Aircraft` | 6 | DIGITS_AIRCRAFT |  | 1 | `SeapowerUI.ViewModels/UnitSelectedState.cs:451` |
| 1D - Manuevers (Ownship only except for turn order confirmation ) | `TurnToCourse_Land` | 6 |  |  | 2 | `SeapowerUI.ViewModels/UnitSelectedState.cs:454` |
| 1D - Manuevers (Ownship only except for turn order confirmation ) | `SetSpeed_Ahead_Flank` | 6 |  |  | 1 | — |
| 1D - Manuevers (Ownship only except for turn order confirmation ) | `SetSpeed_Ahead_Full` | 6 |  |  | 1 | — |
| 1D - Manuevers (Ownship only except for turn order confirmation ) | `SetSpeed_Ahead_Standard` | 6 |  |  | 1 | — |
| 1D - Manuevers (Ownship only except for turn order confirmation ) | `SetSpeed_Ahead_2_3` | 6 |  |  | 1 | — |
| 1D - Manuevers (Ownship only except for turn order confirmation ) | `SetSpeed_Ahead_1_3` | 6 |  |  | 1 | — |
| 1D - Manuevers (Ownship only except for turn order confirmation ) | `SetSpeed_AllStop` | 6 |  |  | 1 | — |
| 1D - Manuevers (Ownship only except for turn order confirmation ) | `SetSpeed_BackFull` | 6 |  |  | 1 | — |
| 1D - Manuevers (Ownship only except for turn order confirmation ) | `OnStation` | 6 |  |  | 1 | `Seapower/GoToRelativeToStationPoint.cs:125`, `Seapower/GoToWaypointTask.cs:387` |
| 1D - Manuevers (Ownship only except for turn order confirmation ) | `HelmAye` |  |  |  | 1 | — |
| 1D - Manuevers (Ownship only except for turn order confirmation ) | `DiveAye` |  |  |  | 1 | — |
| 1E - Sensors (Ownship only) | `ActiveSonarOn` | 6 |  |  | 1 | `Seapower/ObjectBase.cs:11148`, `Seapower/SensorSystemSonar.cs:83` |
| 1E - Sensors (Ownship only) | `ActiveSonarOff` | 6 |  |  | 1 | `Seapower/ObjectBase.cs:11178`, `Seapower/SensorSystemSonar.cs:87` |
| 1E - Sensors (Ownship only) | `VDSDeploy` | 6 |  |  | 1 | `Seapower/SensorSystem.cs:893` |
| 1E - Sensors (Ownship only) | `VDSRetract` | 6 |  |  | 1 | `Seapower/SensorSystem.cs:989` |
| 1E - Sensors (Ownship only) | `TADeploy` | 6 |  |  | 1 | `Seapower/SensorSystem.cs:897` |
| 1E - Sensors (Ownship only) | `TARetract` | 6 |  |  | 1 | `Seapower/SensorSystem.cs:993` |
| 1E - Sensors (Ownship only) | `AirRadarOn` | 6 |  |  | 1 | `SeapowerUI.ViewModels/ObjectSensorViewModel.cs:105` |
| 1E - Sensors (Ownship only) | `AirRadarOff` | 6 |  |  | 1 | `SeapowerUI.ViewModels/ObjectSensorViewModel.cs:109` |
| 1E - Sensors (Ownship only) | `SurfaceRadarOn` | 6 |  |  | 1 | `SeapowerUI.ViewModels/ObjectSensorViewModel.cs:138` |
| 1E - Sensors (Ownship only) | `SurfaceRadarOff` | 6 |  |  | 1 | `SeapowerUI.ViewModels/ObjectSensorViewModel.cs:142` |
| 1E - Sensors (Ownship only) | `EMCON` | 6 |  |  | 1 | `Seapower/ObjectBase.cs:6096` |
| 1F - Contacts (Taskforce level) | `New` | 4 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `NewF` | 4 |  |  | 1 | `Seapower/Taskforce.cs:2281` |
| 1F - Contacts (Taskforce level) | `Multiple` | 4 |  |  | 1 | `Seapower/Taskforce.cs:2202`, `Seapower/Taskforce.cs:2368` |
| 1F - Contacts (Taskforce level) | `ClassifiedHostileAir` | 5 |  |  | 1 | `Seapower/Taskforce.cs:2338`, `Seapower/Taskforce.cs:2392` |
| 1F - Contacts (Taskforce level) | `ClassifiedHostileAirMultiple` | 5 |  |  | 1 | `Seapower/Taskforce.cs:2329` |
| 1F - Contacts (Taskforce level) | `ContactAir` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `ContactAirF` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `ContactAirM` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `ContactLand` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `ContactLandF` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `ContactLandM` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `ContactSurface` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `ContactSurfaceF` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `ContactSurfaceM` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `ContactSubmerged` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `ContactSubmergedF` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `ContactSubmergedM` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `Contact` | 4 |  |  | 1 | `Seapower/Taskforce.cs:2283` |
| 1F - Contacts (Taskforce level) | `Contacts` | 4 |  |  | 1 | `Seapower/Taskforce.cs:2207`, `Seapower/Taskforce.cs:2377` |
| 1F - Contacts (Taskforce level) | `NewContact` | 4 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `NewContactMultiple` | 4 |  |  | 1 | `Seapower/Taskforce.cs:2198` |
| 1F - Contacts (Taskforce level) | `ESMContact` | 5 | DIGITS |  | 1 | — |
| 1F - Contacts (Taskforce level) | `RadarContact` | 5 | DesignateTrack |  | 1 | — |
| 1F - Contacts (Taskforce level) | `PassiveContact` | 5 | DesignateTrack |  | 1 | — |
| 1F - Contacts (Taskforce level) | `ActiveContact` | 5 | DesignateTrack |  | 1 | — |
| 1F - Contacts (Taskforce level) | `VisualContact` | 5 | DesignateTrack |  | 1 | — |
| 1F - Contacts (Taskforce level) | `RadarEmission` | 5 | DesignateTrack |  | 1 | — |
| 1F - Contacts (Taskforce level) | `DesignateTrack` | 5 | DIGITS |  | 1 | — |
| 1F - Contacts (Taskforce level) | `LaunchTransient` | 5 | DIGITS |  | 1 | — |
| 1F - Contacts (Taskforce level) | `ContactFaded` | 5 | DIGITS |  | 1 | `Seapower/Taskforce.cs:2610` |
| 1F - Contacts (Taskforce level) | `AirTarget` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `SeaTarget` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `LandTarget` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `Track` | 5 |  |  | 1 | `Seapower/WeaponBase.cs:2423` |
| 1F - Contacts (Taskforce level) | `TrackNumber` | 5 | DIGITS |  | 1 | `Seapower/Taskforce.cs:2576` |
| 1F - Contacts (Taskforce level) | `IsClassified` | 5 |  |  | 1 | `Seapower/Taskforce.cs:2577` |
| 1F - Contacts (Taskforce level) | `Friendly` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `Neutral` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `Hostile` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `FriendlyF` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `NeutralF` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `HostileF` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `FriendlyM` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `NeutralM` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `HostileM` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `FriendlyS` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `NeutralS` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `HostileS` | 5 |  |  | 1 | `Seapower/Taskforce.cs:2371` |
| 1F - Contacts (Taskforce level) | `Aircraft` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `Helicopter` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `Vessel` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `CivShip` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `Airbase` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `SAMSite` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `MissileSite` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `Building` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `Port` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `Radar` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `LandVehicle` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `Ground` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `SubmergedSubmarine` | 5 |  |  | 1 | — |
| 1F - Contacts (Taskforce level) | `Biologic` | 5 |  |  | 1 | — |
| 1G - Weapons | `EngageTrack` | 4 | DIGITS |  | 2 | `Seapower/AI.cs:2950`, `Seapower/AttackTask.cs:26` |
| 1G - Weapons | `SAMReady` | 4 |  |  | 1 | — |
| 1G - Weapons | `SAMFired` | 4 | DIGITS |  | 1 | — |
| 1G - Weapons | `ASHMFired` | 4 | DIGITS |  | 1 | — |
| 1G - Weapons | `ASUMFired` | 4 | DIGITS |  | 1 | — |
| 1G - Weapons | `SalvoFire` | 4 |  |  | 1 | `Seapower/AI.cs:2953`, `Seapower/AttackTask.cs:29` |
| 1G - Weapons | `LaunchAtTarget` | 4 | DIGITS |  | 1 | — |
| 1G - Weapons | `HoldFireTrack` | 4 | DIGITS |  | 1 | — |
| 1G - Weapons | `CeaseFire` | 4 |  |  | 1 | `Seapower/ObjectBase.cs:3176` |
| 1G - Weapons | `ChaffOut` | 4 |  |  | 1 | `Seapower/ObjectBase.cs:9671` |
| 1G - Weapons | `DecoyOut` | 4 |  |  | 1 | `Seapower/WeaponSystemLauncher.cs:839` |
| 1G - Weapons | `TargetRequired` | 4 |  |  | 1 | `SeapowerUI.ViewModels/AttackingState.cs:482`, `SeapowerUI.ViewModels/AttackingWithSystemState.cs:114`, `SeapowerUI.ViewModels/IdentifyState.cs:75` |
| 1G - Weapons | `WrongTarget` | 4 |  |  | 1 | `Seapower/InputHandler.cs:1692`, `Seapower/InputHandler.cs:1713`, `SeapowerUI.ViewModels/AttackingState.cs:473`, `SeapowerUI.ViewModels/SonobuoyLineState.cs:68` |
| 1G - Weapons | `Jam` | 4 | DIGITS |  | 1 | `Seapower/JamTask.cs:73` |
| 1G - Weapons | `DesignateWithLaser` | 4 | DIGITS |  | 1 | `Seapower/HighlightWithLaserTask.cs:91` |
| 1G - Weapons | `StopJam` | 4 | DIGITS |  | 1 | `Seapower/JamTask.cs:45` |
| 1G - Weapons | `StopLaser` | 4 | DIGITS |  | 1 | `Seapower/HighlightWithLaserTask.cs:38` |
| 1G - Weapons | `CannotJam` | 4 | DIGITS |  | 1 | — |
| 1G - Weapons | `CannotLaser` | 4 | DIGITS |  | 1 | `Seapower/HighlightWithLaserTask.cs:100` |
| 1H - Kills | `SplashedTrack` | 3 | DIGITS_EXCITED |  | 4 | `Seapower/Blastzone.cs:861`, `Seapower/Projectile.cs:783`, `Seapower/WeaponSystemCIWS.cs:1841` |
| 1H - Kills | `HitTrack` | 3 | DIGITS_EXCITED |  | 1 | `Seapower/Blastzone.cs:887` |
| 1H - Kills | `IsHistory` |  |  |  | 1 | — |
| 1H - Kills | `IsToast` |  |  |  | 1 | — |
| 1I - Sustained Damage | `WeHit` | 2 |  |  | 4 | `Seapower/Blastzone.cs:959`, `Seapower/Projectile.cs:811` |
| 1I - Sustained Damage | `DamagedNeedHelp` | 2 |  |  | 1 | — |
| 1I - Sustained Damage | `AbandonShip` | 1 |  |  | 2 | `Seapower/Blastzone.cs:1424` |
| 1I - Sustained Damage | `FloodingAbandonShip` | 1 |  |  | 1 | `Seapower/Compartments.cs:1924` |
| 1J - Air Ops | `HeloLaunching` | 6 |  |  | 1 | — |
| 1J - Air Ops | `HeloLaunched` | 6 |  |  | 1 | — |
| 1J - Air Ops | `HeloRecovered` | 6 |  |  | 1 | — |
| 1J - Air Ops | `AircraftLaunching` | 6 |  |  | 1 | — |
| 1J - Air Ops | `AllAircraftLaunched` | 6 |  |  | 1 | — |
| 1J - Air Ops | `AllAircraftRecovered` | 6 |  |  | 1 | — |
| 1J - Air Ops | `CarrierDeckDamaged` | 6 |  |  | 1 | — |
| 1J - Air Ops | `ColdCat` | 6 |  |  | 1 | — |
| 1J - Air Ops | `WaveOff` | 6   //Carrier landing fail |  |  | 1 | — |
| 1K - Submarines | `MakeMyDepth` | 6 | DIGITS | DiveAye | 1 | `SeapowerUI.ViewModels/ObjectSubmarineDepthViewModel.cs:89` |
| 1K - Submarines | `SurfaceTheShip` | 6 |  |  | 1 | — |
| 1K - Submarines | `EmergencySurface` | 6 |  |  | 1 | — |
| 1K - Submarines | `EmergencyDeep` | 6 |  |  | 1 | — |
| 1K - Submarines | `PeriscopeDepth` | 6 |  |  | 1 | — |
| 1K - Submarines | `RaiseScope` | 6 |  |  | 1 | `Seapower/SensorSystem.cs:723` |
| 1K - Submarines | `LowerScope` | 6 |  |  | 1 | `Seapower/SensorSystem.cs:974` |
| 1K - Submarines | `RaiseESM` | 6 |  |  | 1 | `Seapower/SensorSystem.cs:731` |
| 1K - Submarines | `LowerESM` | 6 |  |  | 1 | `Seapower/SensorSystem.cs:982` |
| 1K - Submarines | `RaiseRadar` | 6 |  |  | 1 | `Seapower/SensorSystem.cs:727` |
| 1K - Submarines | `LowerRadar` | 6 |  |  | 1 | `Seapower/SensorSystem.cs:978` |
| 1K - Submarines | `RaiseSnorkel` | 6 |  |  | 1 | `Seapower/SnorkelMastSystem.cs:103` |
| 1K - Submarines | `LowerSnorkel` | 6 |  |  | 1 | `Seapower/SnorkelMastSystem.cs:114` |
| 1K - Submarines | `SilentRunning` | 6 |  |  | 1 | — |
| 1K - Submarines | `BatteryLow` | 6 |  |  | 1 | `Seapower/Submarine.cs:519` |
| 1K - Submarines | `NeedToChargeBatteries` | 6 |  |  | 1 | `Seapower/Submarine.cs:490` |
| 1L - Enemy actions | `MissileIncoming_Surface` | 2 | DIGITS_EXCITED |  | 1 | `Seapower/Vessel.cs:1386` |
| 1L - Enemy actions | `TargetedByRadar` | 2 | DIGITS |  | 1 | `Seapower/ObjectBase.cs:1964` |
| 1L - Enemy actions | `TorpedoIncoming` | 2 | DIGITS |  | 1 | `Seapower/ObjectBase.cs:1710` |
| 1L - Enemy actions | `UnderFire` | 2 |  |  | 1 | `Seapower/ObjectBase.cs:1972` |
| 1M - ROE | `WeaponsFree` | 7 |  |  | 1 | — |
| 1M - ROE | `WeaponsTight` | 7 |  |  | 1 | — |
| 1M - ROE | `WeaponsSafe` | 7 |  |  | 1 | — |
| 2A - Aircraft Orders | `Affirmative` | 7 |  |  | 1 | — |
| 2A - Aircraft Orders | `NegativeAir` | 7 |  |  | 1 | — |
| 2A - Aircraft Orders | `ReturnToBase` | 6 |  |  | 1 | `Seapower/Order.cs:144` |
| 2A - Aircraft Orders | `BingoFuel` | 6 |  |  | 1 | — |
| 2A - Aircraft Orders | `SetSpeed_Loiter` | 6 |  |  | 1 | — |
| 2A - Aircraft Orders | `SetSpeed_Cruise` | 6 |  |  | 1 | — |
| 2A - Aircraft Orders | `SetSpeed_Max` | 6 |  |  | 1 | — |
| 2A - Aircraft Orders | `SetSpeed_Afterburner` | 6 |  |  | 1 | — |
| 2A - Aircraft Orders | `DropFueltanks` | 5 |  |  | 1 | — |
| 2A - Aircraft Orders | `DropFueltanksCombat` | 5 |  |  | 1 | — |
| 2A - Aircraft Orders | `VisualIdentify` | 4 | DIGITS |  | 1 | `Seapower/VisualIdentificationTask.cs:25`, `SeapowerUI.ViewModels/IdentifyState.cs:60`, `SeapowerUI.ViewModels/UnitSelectedState.cs:782` |
| 2A - Aircraft Orders | `SetAltitude` | 6 | DIGITS |  | 1 | `Seapower/Helicopter.cs:1301`, `SeapowerUI.ViewModels/ObjectAirVehicleAltitudeViewModel.cs:95` |
| 2A - Aircraft Orders | `CannotComplyNoAmmo` | 6 |  |  | 1 | — |
| 2A - Aircraft Orders | `FuelRTB` | 6 |  |  | 1 | — |
| 2A - Aircraft Orders | `FuelRTBAuto` | 6 |  |  | 1 | — |
| 2A - Aircraft Orders | `ClassifyTrack` | 4 | FULL |  | 1 | `Seapower/VisualIdentificationTask.cs:19` |
| 2B - Aircraft Contacts | `BouyContact` | 5 | DesignateTrack |  | 1 | — |
| 2B - Aircraft Contacts | `DippingSonarDeploy` | 6 |  |  | 1 | — |
| 2B - Aircraft Contacts | `DippingSonarRetract` | 6 |  |  | 1 | — |
| 2B - Aircraft Contacts | `MADContact` | 5 | DesignateTrack |  | 1 | — |
| 2B - Aircraft Contacts | `BeingJammed` | 5 |  |  | 1 | `Seapower/ObjectBase.cs:1945` |
| 2C - Aircraft Combat | `EvasiveManuever` | 2 |  |  | 1 | — |
| 2C - Aircraft Combat | `MissileIncoming` | 2 |  |  | 1 | `Seapower/Aircraft.cs:1787`, `Seapower/ObjectBase.cs:1928` |
| 2C - Aircraft Combat | `MissileIncoming_Sam` | 2 |  |  | 1 | `Seapower/Aircraft.cs:1783` |
| 2D - Aircraft Weapons | `AircraftGunsFired` | 4 |  |  | 1 | `Seapower/WeaponSystemCIWS.cs:2182` |
| 2D - Aircraft Weapons | `AAM_SARH_Fired` | 4 | DIGITS_AIRCRAFT |  | 1 | — |
| 2D - Aircraft Weapons | `AAM_IR_Fired` | 4 | DIGITS_AIRCRAFT |  | 1 | — |
| 2D - Aircraft Weapons | `AAM_ARS_Fired` | 4 | DIGITS_AIRCRAFT |  | 1 | — |
| 2D - Aircraft Weapons | `BombsAway` | 4 | DIGITS_AIRCRAFT |  | 1 | — |
| 2D - Aircraft Weapons | `BuoyAway` | 4 | DIGITS_AIRCRAFT |  | 1 | — |
| 2D - Aircraft Weapons | `ARMFiredAir` | 4 | DIGITS_AIRCRAFT |  | 1 | — |
| 2D - Aircraft Weapons | `TorpedoFiredAir` | 4 | DIGITS_AIRCRAFT |  | 1 | — |
| 2D - Aircraft Weapons | `WeaponAway` | 4 | DIGITS_AIRCRAFT |  | 1 | — |
| 2D - Aircraft Weapons | `Winchester` | 6 |  |  | 1 | — |
| 2E - Aircraft Deaths | `ImGoingDown` | 1 |  |  | 2 | — |
| 2F - Aircraft Kills | `AirHitSurface` | 4 |  |  | 1 | — |
| 2F - Aircraft Kills | `AirHitLand` | 4 |  |  | 1 | — |
| 2F - Aircraft Kills | `AirHitAir` | 4 |  |  | 1 | — |
| 3A - Tasking updates | `NewIntelReceived` | 2 |  |  | 1 | `Seapower/EventLog.cs:101` |
| 3A - Tasking updates | `NewTaskReceived` | 2 |  |  | 1 | — |
| 3B - Mission  updates | `MissionComplete` | 2 |  |  | 1 | — |
| 3B - Mission  updates | `MissionFail` | 2 |  |  | 1 | — |
| 3B - Mission  updates | `ObjectiveComplete` | 2 |  |  | 1 | — |
| 3B - Mission  updates | `ObjectiveFailed` | 2 |  |  | 1 | — |

## Keys called in code but not in [US] (dynamic / other sections / missing)

- `ChangeCourse` – `Seapower/Order.cs:121`
- `ClassifiedContactMultiple` – `Seapower/Taskforce.cs:2363`
- `ClassifiedHostileMultiple` – `Seapower/Taskforce.cs:2359`

## Dynamic (non-literal) call sites

Key built at runtime – the StockVoiceSuppressor sees the final key string, so these are covered too:

- AttackTask.cs:47 (weapon-type fire calls, e.g. AAM_SARH_Fired / AAM_IR_Fired / SAMFired / ASHMFired)
- ObjectBase.cs:7166, ObjectBase.cs:9719 (unit state / countermeasure lines)
- Submarine.cs:872, Submarine.cs:1207 (depth / telegraph)
- Vessel.cs:620 (SetSpeed_* telegraph)
- WeaponBase.cs:2401, WeaponBase.cs:2417 (weapon away / AirHit* / Track)
- Taskforce.cs:2205, 2282, 2375, 2578-2579 (contact report sequences: Contact* / Hostile* / class names)
- Trigger.cs:3605 (mission-editor trigger: arbitrary key + nation)
- ObjectAltitude.cs:27/50 (PlayAircraftAltitude), ObjectDepth.cs:33, ObjectSpeed.cs:162 (SetSpeed_*)
- ObjectSensorViewModel.cs:331/403/461 (sensor on/off), SonobuoyLineState.cs:225 (BuoyAway)
