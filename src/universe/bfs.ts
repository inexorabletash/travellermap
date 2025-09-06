import {Universe} from "../universe.js";
import {World} from "./world.js";
import {hexRadius} from "../render/border.js";


const MAX_ITERATIONS = 30;

export function worldBfs(universe: Universe, from: World, to: World, jump: number, worldValid: (w: World) => boolean) {
    const targets: Map<World,World> = new Map();
    let active: Set<World> = new Set([to]);

    if(to === from) {
        return [to];
    }

    for(let iter = 0; iter < MAX_ITERATIONS && active.size > 0; ++iter) {
        const nextActive = new Set<World>();
        for(const [fromWorld, toWorld] of activeJumpIterator(universe, active, jump, worldValid)) {
            if(!targets.has(toWorld)) {
                targets.set(toWorld, fromWorld);
                nextActive.add(toWorld);
            }
        }
        if(targets.has(from)) {
            break;
        }
        active = nextActive;
    }

    if(!targets.has(from)) {
        return undefined;
    }
    let result = [];
    for(let world = from; world !== to; world = targets.get(world) as World) {
        result.push(world);
    }
    result.push(to);
    return result;
}


function* activeJumpIterator(universe: Universe, active: Set<World>, jump: number, worldValid: (w: World) => boolean) {
    const visited: Set<World> = new Set(active);
    let count = 0;
    for(const world of active) {
        const hops = hexRadius(world.globalCoords, jump, true);
        for(const hop of hops) {
            const hopWorld = universe.lookupWorld(hop[0], hop[1]);
            if(hopWorld !== undefined && !visited.has(hopWorld) && worldValid(hopWorld)) {
                visited.add(hopWorld);
                ++count;
                yield [world, hopWorld];
            }
        }
    }
    return count;
}