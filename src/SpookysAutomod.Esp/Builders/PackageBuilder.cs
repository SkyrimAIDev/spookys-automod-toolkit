using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace SpookysAutomod.Esp.Builders;

/// <summary>
/// Fluent builder for creating Package records (AI behavior packages for NPCs).
/// Now with full Mutagen API support for proper package structure including
/// Data dictionary, ProcedureTree, and complete package configuration.
/// </summary>
public class PackageBuilder
{
    private readonly SkyrimMod _mod;
    private readonly Package _package;
    private byte _nextDataIndex = 0;

    public PackageBuilder(SkyrimMod mod, string editorId)
    {
        _mod = mod;
        _package = mod.Packages.AddNew();
        _package.EditorID = editorId;

        // Initialize with sensible defaults
        _package.Flags = Package.Flag.OffersServices;
        _package.InterruptOverride = Package.Interrupt.None;
        _package.PreferredSpeed = Package.Speed.Walk;
    }

    /// <summary>
    /// Configure package as Sandbox type for general wandering/idling in an area.
    /// </summary>
    /// <param name="radius">Radius in units around NPC's current location (default: 500)</param>
    public PackageBuilder AsSandbox(uint radius = 500)
    {
        // Set flags for sandbox behavior
        _package.Flags |= Package.Flag.AllowSwimming;
        _package.InteruptFlags |= Package.InterruptFlag.AllowIdleChatter;
        _package.InteruptFlags |= Package.InterruptFlag.WorldInteractions;

        // Add location data - sandbox around current location
        var locationData = new PackageDataLocation
        {
            Name = "SandboxLocation",
            Location = new LocationTargetRadius
            {
                Target = new LocationTarget(), // Empty = near self
                Radius = radius
            }
        };
        var locIndex = _nextDataIndex++;
        _package.Data[(sbyte)locIndex] = locationData;

        // Add procedure branch for sandbox behavior
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Sandbox"
        };
        branch.DataInputIndices.Add(locIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Travel type for moving to a destination.
    /// </summary>
    /// <param name="destinationRef">FormKey of destination marker/reference</param>
    public PackageBuilder AsTravel(FormKey destinationRef)
    {
        // Add target data for travel destination
        var targetData = new PackageDataTarget
        {
            Name = "TravelDestination",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = destinationRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch for travel
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Travel"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Sleep type for sleeping in a bed.
    /// </summary>
    /// <param name="bedRef">FormKey of bed furniture reference</param>
    /// <param name="startHour">Hour to start sleeping (0-23, default: 22 for 10 PM)</param>
    /// <param name="duration">Hours to sleep (default: 8)</param>
    public PackageBuilder AsSleep(FormKey bedRef, byte startHour = 22, byte duration = 8)
    {
        if (startHour > 23)
            throw new ArgumentException("Start hour must be 0-23", nameof(startHour));

        if (duration == 0 || duration > 24)
            throw new ArgumentException("Duration must be 1-24 hours", nameof(duration));

        // Set schedule
        _package.ScheduleHour = (sbyte)startHour;
        _package.ScheduleDurationInMinutes = duration * 60;

        // Add special sleep flag
        _package.Flags |= Package.Flag.WearSleepOutfit;

        // Add target data for bed
        var targetData = new PackageDataTarget
        {
            Name = "SleepFurniture",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = bedRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch for sleep
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Sleep"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Eat type for eating at furniture.
    /// </summary>
    /// <param name="furnitureRef">FormKey of chair/table furniture reference</param>
    /// <param name="startHour">Hour to start eating (0-23, default: 12 for noon)</param>
    /// <param name="duration">Hours to eat (default: 2)</param>
    public PackageBuilder AsEat(FormKey furnitureRef, byte startHour = 12, byte duration = 2)
    {
        if (startHour > 23)
            throw new ArgumentException("Start hour must be 0-23", nameof(startHour));

        if (duration == 0 || duration > 24)
            throw new ArgumentException("Duration must be 1-24 hours", nameof(duration));

        // Set schedule
        _package.ScheduleHour = (sbyte)startHour;
        _package.ScheduleDurationInMinutes = duration * 60;

        // Add target data for furniture
        var targetData = new PackageDataTarget
        {
            Name = "EatFurniture",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = furnitureRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch for eat
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Eat"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Follow type for following an actor.
    /// </summary>
    /// <param name="targetRef">FormKey of actor to follow</param>
    /// <param name="followDistance">Distance to maintain from target (default: 200 units)</param>
    public PackageBuilder AsFollow(FormKey targetRef, ushort followDistance = 200)
    {
        // Add target data for follow target
        var targetData = new PackageDataTarget
        {
            Name = "FollowTarget",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = targetRef.ToLink<IPlacedGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch for follow
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Follow"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Guard type for guarding a location.
    /// </summary>
    /// <param name="markerRef">FormKey of guard position marker/reference</param>
    public PackageBuilder AsGuard(FormKey markerRef)
    {
        // Add target data for guard position
        var targetData = new PackageDataTarget
        {
            Name = "GuardPosition",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = markerRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch for guard
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Guard"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        // Guards should always have weapons drawn
        _package.Flags |= Package.Flag.WeaponDrawn;

        return this;
    }

    /// <summary>
    /// Configure package as Patrol type for patrolling to a marker.
    /// Note: Full multi-point patrol requires more complex configuration.
    /// </summary>
    /// <param name="patrolMarker">FormKey of patrol point marker</param>
    public PackageBuilder AsPatrol(FormKey patrolMarker)
    {
        // Add target data for patrol point
        var targetData = new PackageDataTarget
        {
            Name = "PatrolPoint",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = patrolMarker.ToLink<IPlacedObjectGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch for patrol
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Patrol"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as UseItemAt type for using/activating objects.
    /// NPCs will use crafting stations, cook at pots, work at forges, etc.
    /// </summary>
    /// <param name="itemRef">FormKey of object to use/activate (forge, cooking pot, etc.)</param>
    public PackageBuilder AsUseItemAt(FormKey itemRef)
    {
        // Add target data for item to use
        var targetData = new PackageDataTarget
        {
            Name = "UseItem",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = itemRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "UseItemAt"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Sit type for sitting at furniture.
    /// NPCs will sit at chairs, benches, etc.
    /// </summary>
    /// <param name="furnitureRef">FormKey of chair/bench to sit at</param>
    public PackageBuilder AsSit(FormKey furnitureRef)
    {
        // Add target data for furniture
        var targetData = new PackageDataTarget
        {
            Name = "SitFurniture",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = furnitureRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Sit"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as UseIdleMarker type for ambient activities.
    /// NPCs will sweep, lean, hammer, chop wood, etc. at idle markers.
    /// </summary>
    /// <param name="idleMarkerRef">FormKey of idle marker</param>
    public PackageBuilder AsUseIdleMarker(FormKey idleMarkerRef)
    {
        // Add target data for idle marker
        var targetData = new PackageDataTarget
        {
            Name = "IdleMarker",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = idleMarkerRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "UseIdleMarker"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Flee type for fleeing from danger.
    /// </summary>
    /// <param name="fleeFromRef">FormKey of what to flee from (optional, can flee from combat)</param>
    /// <param name="fleeDistance">Distance to flee (default: 1000 units)</param>
    public PackageBuilder AsFlee(FormKey? fleeFromRef = null, ushort fleeDistance = 1000)
    {
        if (fleeFromRef.HasValue)
        {
            // Add target data for what to flee from
            var targetData = new PackageDataTarget
            {
                Name = "FleeFrom",
                Type = PackageDataTarget.Types.SingleRef,
                Target = new PackageTargetSpecificReference
                {
                    Reference = fleeFromRef.Value.ToLink<IPlacedGetter>()
                }
            };
            var targetIndex = _nextDataIndex++;
            _package.Data[(sbyte)targetIndex] = targetData;

            var branch = new PackageBranch
            {
                BranchType = "0",
                ProcedureType = "Flee"
            };
            branch.DataInputIndices.Add(targetIndex);
            _package.ProcedureTree.Add(branch);
        }
        else
        {
            // Flee from combat (no specific target)
            var branch = new PackageBranch
            {
                BranchType = "0",
                ProcedureType = "Flee"
            };
            _package.ProcedureTree.Add(branch);
        }

        // Remove combat flags so NPC will flee
        _package.Flags &= ~Package.Flag.IgnoreCombat;

        return this;
    }

    /// <summary>
    /// Configure package as Accompany type for accompanying an actor to a destination.
    /// Similar to Follow but stops at destination.
    /// </summary>
    /// <param name="targetRef">FormKey of actor to accompany</param>
    /// <param name="destinationRef">FormKey of destination marker</param>
    public PackageBuilder AsAccompany(FormKey targetRef, FormKey destinationRef)
    {
        // Add target data
        var targetData = new PackageDataTarget
        {
            Name = "AccompanyTarget",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = targetRef.ToLink<IPlacedGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add destination data
        var destData = new PackageDataTarget
        {
            Name = "Destination",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = destinationRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var destIndex = _nextDataIndex++;
        _package.Data[(sbyte)destIndex] = destData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Accompany"
        };
        branch.DataInputIndices.Add(targetIndex);
        branch.DataInputIndices.Add(destIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as CastMagic type for casting spells.
    /// </summary>
    /// <param name="targetRef">FormKey of target location/actor</param>
    public PackageBuilder AsCastMagic(FormKey targetRef)
    {
        // Add target data
        var targetData = new PackageDataTarget
        {
            Name = "CastTarget",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = targetRef.ToLink<IPlacedGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "CastMagic"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Dialogue type for engaging in dialogue.
    /// </summary>
    /// <param name="targetRef">FormKey of dialogue target</param>
    public PackageBuilder AsDialogue(FormKey targetRef)
    {
        // Add target data
        var targetData = new PackageDataTarget
        {
            Name = "DialogueTarget",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = targetRef.ToLink<IPlacedGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Dialogue"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Find type for searching for something.
    /// </summary>
    /// <param name="targetRef">FormKey of what to find</param>
    public PackageBuilder AsFind(FormKey targetRef)
    {
        // Add target data
        var targetData = new PackageDataTarget
        {
            Name = "FindTarget",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = targetRef.ToLink<IPlacedGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Find"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Ambush type for waiting in ambush.
    /// </summary>
    /// <param name="ambushMarkerRef">FormKey of ambush position marker</param>
    public PackageBuilder AsAmbush(FormKey ambushMarkerRef)
    {
        // Add target data for ambush position
        var targetData = new PackageDataTarget
        {
            Name = "AmbushPosition",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = ambushMarkerRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Ambush"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        // Set stealth flag
        _package.Flags |= Package.Flag.AlwaysSneak;

        return this;
    }

    /// <summary>
    /// Configure package as Wander type for wandering within a radius.
    /// </summary>
    /// <param name="wanderMarkerRef">FormKey of wander center marker</param>
    /// <param name="radius">Wander radius in units (default: 1000)</param>
    public PackageBuilder AsWander(FormKey wanderMarkerRef, ushort radius = 1000)
    {
        // Add location data for wander center
        var locationData = new PackageDataLocation
        {
            Name = "WanderLocation",
            Location = new LocationTargetRadius
            {
                Target = new LocationTarget
                {
                    Link = wanderMarkerRef.ToLink<IPlacedGetter>()
                },
                Radius = radius
            }
        };
        var locationIndex = _nextDataIndex++;
        _package.Data[(sbyte)locationIndex] = locationData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Wander"
        };
        branch.DataInputIndices.Add(locationIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Wait type for waiting at a location.
    /// </summary>
    /// <param name="waitMarkerRef">FormKey of wait position marker</param>
    public PackageBuilder AsWait(FormKey waitMarkerRef)
    {
        // Add target data for wait position
        var targetData = new PackageDataTarget
        {
            Name = "WaitPosition",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = waitMarkerRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Wait"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Activate type for activating a specific object.
    /// </summary>
    /// <param name="activateRef">FormKey of object to activate</param>
    public PackageBuilder AsActivate(FormKey activateRef)
    {
        // Add target data for object to activate
        var targetData = new PackageDataTarget
        {
            Name = "ActivateTarget",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = activateRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Activate"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Relax type for relaxing at a location.
    /// </summary>
    /// <param name="relaxMarkerRef">FormKey of relax location marker</param>
    public PackageBuilder AsRelax(FormKey relaxMarkerRef)
    {
        // Add target data for relax position
        var targetData = new PackageDataTarget
        {
            Name = "RelaxPosition",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = relaxMarkerRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Relax"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as ForceGreet type for force greeting an actor.
    /// </summary>
    /// <param name="targetRef">FormKey of actor to force greet</param>
    public PackageBuilder AsForceGreet(FormKey targetRef)
    {
        // Add target data for greet target
        var targetData = new PackageDataTarget
        {
            Name = "GreetTarget",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = targetRef.ToLink<IPlacedGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "ForceGreet"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Greet type for standard greeting behavior.
    /// </summary>
    /// <param name="targetRef">Optional FormKey of actor to greet</param>
    public PackageBuilder AsGreet(FormKey? targetRef = null)
    {
        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Greet"
        };

        // If target specified, add target data
        if (targetRef.HasValue)
        {
            var targetData = new PackageDataTarget
            {
                Name = "GreetTarget",
                Type = PackageDataTarget.Types.SingleRef,
                Target = new PackageTargetSpecificReference
                {
                    Reference = targetRef.Value.ToLink<IPlacedGetter>()
                }
            };
            var targetIndex = _nextDataIndex++;
            _package.Data[(sbyte)targetIndex] = targetData;
            branch.DataInputIndices.Add(targetIndex);
        }

        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as UseWeapon type for using a specific weapon.
    /// </summary>
    /// <param name="weaponRef">FormKey of weapon to use</param>
    /// <param name="targetRef">Optional FormKey of target to attack</param>
    public PackageBuilder AsUseWeapon(FormKey weaponRef, FormKey? targetRef = null)
    {
        // Add weapon data
        var weaponData = new PackageDataTarget
        {
            Name = "Weapon",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = weaponRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var weaponIndex = _nextDataIndex++;
        _package.Data[(sbyte)weaponIndex] = weaponData;

        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "UseWeapon"
        };
        branch.DataInputIndices.Add(weaponIndex);

        // Add target if specified
        if (targetRef.HasValue)
        {
            var targetData = new PackageDataTarget
            {
                Name = "Target",
                Type = PackageDataTarget.Types.SingleRef,
                Target = new PackageTargetSpecificReference
                {
                    Reference = targetRef.Value.ToLink<IPlacedGetter>()
                }
            };
            var targetIndex = _nextDataIndex++;
            _package.Data[(sbyte)targetIndex] = targetData;
            branch.DataInputIndices.Add(targetIndex);
        }

        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as UseMagic type for casting a spell.
    /// </summary>
    /// <param name="spellRef">FormKey of spell to cast</param>
    /// <param name="targetRef">Optional FormKey of target</param>
    public PackageBuilder AsUseMagic(FormKey spellRef, FormKey? targetRef = null)
    {
        // Add spell data
        var spellData = new PackageDataTarget
        {
            Name = "Spell",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = spellRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var spellIndex = _nextDataIndex++;
        _package.Data[(sbyte)spellIndex] = spellData;

        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "UseMagic"
        };
        branch.DataInputIndices.Add(spellIndex);

        // Add target if specified
        if (targetRef.HasValue)
        {
            var targetData = new PackageDataTarget
            {
                Name = "Target",
                Type = PackageDataTarget.Types.SingleRef,
                Target = new PackageTargetSpecificReference
                {
                    Reference = targetRef.Value.ToLink<IPlacedGetter>()
                }
            };
            var targetIndex = _nextDataIndex++;
            _package.Data[(sbyte)targetIndex] = targetData;
            branch.DataInputIndices.Add(targetIndex);
        }

        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as LockDoors type for locking doors on schedule.
    /// </summary>
    /// <param name="doorRef">Optional FormKey of specific door to lock</param>
    public PackageBuilder AsLockDoors(FormKey? doorRef = null)
    {
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "LockDoors"
        };

        // If door specified, add door data
        if (doorRef.HasValue)
        {
            var doorData = new PackageDataTarget
            {
                Name = "Door",
                Type = PackageDataTarget.Types.SingleRef,
                Target = new PackageTargetSpecificReference
                {
                    Reference = doorRef.Value.ToLink<IPlacedObjectGetter>()
                }
            };
            var doorIndex = _nextDataIndex++;
            _package.Data[(sbyte)doorIndex] = doorData;
            branch.DataInputIndices.Add(doorIndex);
        }

        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as UnlockDoors type for unlocking doors on schedule.
    /// </summary>
    /// <param name="doorRef">Optional FormKey of specific door to unlock</param>
    public PackageBuilder AsUnlockDoors(FormKey? doorRef = null)
    {
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "UnlockDoors"
        };

        // If door specified, add door data
        if (doorRef.HasValue)
        {
            var doorData = new PackageDataTarget
            {
                Name = "Door",
                Type = PackageDataTarget.Types.SingleRef,
                Target = new PackageTargetSpecificReference
                {
                    Reference = doorRef.Value.ToLink<IPlacedObjectGetter>()
                }
            };
            var doorIndex = _nextDataIndex++;
            _package.Data[(sbyte)doorIndex] = doorData;
            branch.DataInputIndices.Add(doorIndex);
        }

        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Dismount type for dismounting from a horse.
    /// </summary>
    public PackageBuilder AsDismount()
    {
        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Dismount"
        };
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Acquire type for picking up/acquiring objects.
    /// </summary>
    /// <param name="objectRef">FormKey of object to acquire</param>
    public PackageBuilder AsAcquire(FormKey objectRef)
    {
        // Add target data for object to acquire
        var targetData = new PackageDataTarget
        {
            Name = "AcquireTarget",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = objectRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Acquire"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Escort type for escorting actors to a destination.
    /// </summary>
    /// <param name="escortRef">FormKey of actor to escort</param>
    /// <param name="destinationRef">FormKey of destination</param>
    public PackageBuilder AsEscort(FormKey escortRef, FormKey destinationRef)
    {
        // Add escort target data
        var escortData = new PackageDataTarget
        {
            Name = "EscortTarget",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = escortRef.ToLink<IPlacedGetter>()
            }
        };
        var escortIndex = _nextDataIndex++;
        _package.Data[(sbyte)escortIndex] = escortData;

        // Add destination data
        var destData = new PackageDataTarget
        {
            Name = "Destination",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = destinationRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var destIndex = _nextDataIndex++;
        _package.Data[(sbyte)destIndex] = destData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Escort"
        };
        branch.DataInputIndices.Add(escortIndex);
        branch.DataInputIndices.Add(destIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Say type for speaking dialogue at a location.
    /// </summary>
    /// <param name="topicRef">FormKey of dialogue topic</param>
    /// <param name="locationRef">Optional FormKey of location where to say dialogue</param>
    public PackageBuilder AsSay(FormKey topicRef, FormKey? locationRef = null)
    {
        // Add topic data
        var topicData = new PackageDataTarget
        {
            Name = "Topic",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = topicRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var topicIndex = _nextDataIndex++;
        _package.Data[(sbyte)topicIndex] = topicData;

        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Say"
        };
        branch.DataInputIndices.Add(topicIndex);

        // Add location if specified
        if (locationRef.HasValue)
        {
            var locData = new PackageDataTarget
            {
                Name = "Location",
                Type = PackageDataTarget.Types.SingleRef,
                Target = new PackageTargetSpecificReference
                {
                    Reference = locationRef.Value.ToLink<IPlacedObjectGetter>()
                }
            };
            var locIndex = _nextDataIndex++;
            _package.Data[(sbyte)locIndex] = locData;
            branch.DataInputIndices.Add(locIndex);
        }

        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Shout type for casting a shout.
    /// </summary>
    /// <param name="shoutRef">FormKey of shout to cast</param>
    /// <param name="targetRef">Optional FormKey of target</param>
    public PackageBuilder AsShout(FormKey shoutRef, FormKey? targetRef = null)
    {
        // Add shout data
        var shoutData = new PackageDataTarget
        {
            Name = "Shout",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = shoutRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var shoutIndex = _nextDataIndex++;
        _package.Data[(sbyte)shoutIndex] = shoutData;

        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Shout"
        };
        branch.DataInputIndices.Add(shoutIndex);

        // Add target if specified
        if (targetRef.HasValue)
        {
            var targetData = new PackageDataTarget
            {
                Name = "Target",
                Type = PackageDataTarget.Types.SingleRef,
                Target = new PackageTargetSpecificReference
                {
                    Reference = targetRef.Value.ToLink<IPlacedGetter>()
                }
            };
            var targetIndex = _nextDataIndex++;
            _package.Data[(sbyte)targetIndex] = targetData;
            branch.DataInputIndices.Add(targetIndex);
        }

        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as FollowTo type for following an actor to a destination.
    /// </summary>
    /// <param name="followRef">FormKey of actor to follow</param>
    /// <param name="destinationRef">FormKey of destination</param>
    public PackageBuilder AsFollowTo(FormKey followRef, FormKey destinationRef)
    {
        // Add follow target data
        var followData = new PackageDataTarget
        {
            Name = "FollowTarget",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = followRef.ToLink<IPlacedGetter>()
            }
        };
        var followIndex = _nextDataIndex++;
        _package.Data[(sbyte)followIndex] = followData;

        // Add destination data
        var destData = new PackageDataTarget
        {
            Name = "Destination",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = destinationRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var destIndex = _nextDataIndex++;
        _package.Data[(sbyte)destIndex] = destData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "FollowTo"
        };
        branch.DataInputIndices.Add(followIndex);
        branch.DataInputIndices.Add(destIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as HoldPosition type for holding a specific position.
    /// </summary>
    /// <param name="positionRef">FormKey of position marker</param>
    public PackageBuilder AsHoldPosition(FormKey positionRef)
    {
        // Add position data
        var positionData = new PackageDataTarget
        {
            Name = "Position",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = positionRef.ToLink<IPlacedObjectGetter>()
            }
        };
        var positionIndex = _nextDataIndex++;
        _package.Data[(sbyte)positionIndex] = positionData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "HoldPosition"
        };
        branch.DataInputIndices.Add(positionIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as KeepAnEyeOn type for observing/watching a target.
    /// </summary>
    /// <param name="targetRef">FormKey of target to watch</param>
    public PackageBuilder AsKeepAnEyeOn(FormKey targetRef)
    {
        // Add target data
        var targetData = new PackageDataTarget
        {
            Name = "WatchTarget",
            Type = PackageDataTarget.Types.SingleRef,
            Target = new PackageTargetSpecificReference
            {
                Reference = targetRef.ToLink<IPlacedGetter>()
            }
        };
        var targetIndex = _nextDataIndex++;
        _package.Data[(sbyte)targetIndex] = targetData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "KeepAnEyeOn"
        };
        branch.DataInputIndices.Add(targetIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Hover type for flying creatures to hover/travel in air.
    /// </summary>
    /// <param name="hoverMarkerRef">FormKey of hover location marker</param>
    /// <param name="radius">Hover area radius (default: 1000)</param>
    public PackageBuilder AsHover(FormKey hoverMarkerRef, ushort radius = 1000)
    {
        // Add location data for hover area
        var locationData = new PackageDataLocation
        {
            Name = "HoverLocation",
            Location = new LocationTargetRadius
            {
                Target = new LocationTarget
                {
                    Link = hoverMarkerRef.ToLink<IPlacedGetter>()
                },
                Radius = radius
            }
        };
        var locationIndex = _nextDataIndex++;
        _package.Data[(sbyte)locationIndex] = locationData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Hover"
        };
        branch.DataInputIndices.Add(locationIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Configure package as Orbit type for orbiting around a location or target.
    /// </summary>
    /// <param name="orbitRef">FormKey of location or target to orbit</param>
    /// <param name="radius">Orbit radius (default: 500)</param>
    public PackageBuilder AsOrbit(FormKey orbitRef, ushort radius = 500)
    {
        // Add location data for orbit center
        var locationData = new PackageDataLocation
        {
            Name = "OrbitCenter",
            Location = new LocationTargetRadius
            {
                Target = new LocationTarget
                {
                    Link = orbitRef.ToLink<IPlacedGetter>()
                },
                Radius = radius
            }
        };
        var locationIndex = _nextDataIndex++;
        _package.Data[(sbyte)locationIndex] = locationData;

        // Add procedure branch
        var branch = new PackageBranch
        {
            BranchType = "0",
            ProcedureType = "Orbit"
        };
        branch.DataInputIndices.Add(locationIndex);
        _package.ProcedureTree.Add(branch);

        return this;
    }

    /// <summary>
    /// Set the location where this package operates (for location-based packages).
    /// </summary>
    /// <param name="locationRef">FormKey of location reference</param>
    /// <param name="radius">Radius around location (default: 500)</param>
    public PackageBuilder WithLocation(FormKey locationRef, uint radius = 500)
    {
        // Point the package's existing location entry at the caller's location. A location-based
        // procedure (Sandbox, Wander, ...) already created a PackageDataLocation and linked it via
        // its ProcedureTree branch, so we update that entry in place to keep the link intact.
        //
        // The previous code wrote a brand-new entry to Data[0] only when Data[0] was absent — but
        // every As* method fills Data[0] first, so --location was silently ignored; and a fresh
        // entry would be orphaned (no procedure branch references it).
        foreach (var entry in _package.Data.Values)
        {
            if (entry is PackageDataLocation locationData)
            {
                locationData.Location = new LocationTargetRadius
                {
                    Target = new LocationTarget { Link = locationRef.ToLink<IPlacedGetter>() },
                    // Preserve the radius the As* method chose; fall back to the argument.
                    Radius = (locationData.Location as LocationTargetRadius)?.Radius ?? radius
                };
                return this;
            }
        }

        return this;
    }

    /// <summary>
    /// Set the time schedule for when this package runs.
    /// </summary>
    /// <param name="startHour">Hour to start (0-23)</param>
    /// <param name="duration">Duration in hours</param>
    public PackageBuilder WithSchedule(byte startHour, byte duration)
    {
        if (startHour > 23)
            throw new ArgumentException("Start hour must be 0-23", nameof(startHour));

        if (duration == 0 || duration > 24)
            throw new ArgumentException("Duration must be 1-24 hours", nameof(duration));

        _package.ScheduleHour = (sbyte)startHour;
        _package.ScheduleDurationInMinutes = duration * 60;

        return this;
    }

    /// <summary>
    /// Add conditions that must be met for this package to run.
    /// </summary>
    public PackageBuilder WithConditions(Action<List<Condition>> configure)
    {
        var conditions = new List<Condition>();
        configure(conditions);

        foreach (var condition in conditions)
        {
            _package.Conditions.Add(condition);
        }

        return this;
    }

    /// <summary>
    /// Add a single condition to the package.
    /// </summary>
    public PackageBuilder AddCondition(Condition condition)
    {
        _package.Conditions.Add(condition);
        return this;
    }

    /// <summary>
    /// Set package flags directly.
    /// </summary>
    public PackageBuilder WithFlags(Package.Flag flags)
    {
        _package.Flags = flags;
        return this;
    }

    /// <summary>
    /// Add a flag to existing package flags.
    /// </summary>
    public PackageBuilder AddFlag(Package.Flag flag)
    {
        _package.Flags |= flag;
        return this;
    }

    /// <summary>
    /// Mark this package as interruptible by combat.
    /// </summary>
    public PackageBuilder WithCombatInterrupt()
    {
        _package.InterruptOverride = Package.Interrupt.Combat;
        return this;
    }

    /// <summary>
    /// Mark this package as interruptible by spectator events.
    /// </summary>
    public PackageBuilder WithSpectatorInterrupt()
    {
        _package.InterruptOverride = Package.Interrupt.Spectator;
        return this;
    }

    /// <summary>
    /// Set the preferred speed for this package.
    /// </summary>
    public PackageBuilder WithSpeed(Package.Speed speed)
    {
        _package.PreferredSpeed = speed;
        return this;
    }

    /// <summary>
    /// Configure package to allow NPCs to walk.
    /// </summary>
    public PackageBuilder WithWalkSpeed()
    {
        return WithSpeed(Package.Speed.Walk);
    }

    /// <summary>
    /// Configure package to allow NPCs to run.
    /// </summary>
    public PackageBuilder WithRunSpeed()
    {
        return WithSpeed(Package.Speed.Run);
    }

    /// <summary>
    /// Configure package to allow NPCs to fast walk.
    /// </summary>
    public PackageBuilder WithFastWalkSpeed()
    {
        return WithSpeed(Package.Speed.FastWalk);
    }

    /// <summary>
    /// Build and return the completed package record.
    /// </summary>
    public Package Build() => _package;

    /// <summary>
    /// Build and return the package's FormKey.
    /// </summary>
    public FormKey BuildFormKey() => _package.FormKey;
}
