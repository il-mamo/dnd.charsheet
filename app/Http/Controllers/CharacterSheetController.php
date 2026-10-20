<?php

namespace App\Http\Controllers;

use App\Models\CharacterSheet;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Inertia\Inertia;
use Inertia\Response;

class CharacterSheetController extends Controller
{
    public function show(): Response
    {
        $sheet = CharacterSheet::query()->firstOrCreate(
            ['id' => 1],
            ['data' => $this->defaults()],
        );

        return Inertia::render('CharacterSheet', ['sheet' => $sheet->data]);
    }

    public function update(Request $request): RedirectResponse
    {
        $data = $request->validate([
            'character' => ['required', 'array'],
            'scores' => ['required', 'array'],
            'saves' => ['required', 'array'],
            'trainedSkills' => ['required', 'array'],
            'weapons' => ['required', 'array'],
            'spellSlots' => ['required', 'array'],
        ]);

        CharacterSheet::query()->updateOrCreate(['id' => 1], ['data' => $data]);

        return back();
    }

    /** @return array<string, mixed> */
    private function defaults(): array
    {
        return [
            'character' => [
                'name' => 'Justyn', 'species' => 'Human', 'className' => 'Barbarian',
                'subclass' => 'Path of the Berserker', 'background' => 'Outlander',
                'level' => 5, 'xp' => 5425, 'currentHp' => 41, 'maxHp' => 52,
                'tempHp' => 0, 'armorClass' => 15, 'speed' => 30, 'hitDice' => '5d12',
                'spentHitDice' => 1, 'proficiency' => 3, 'initiative' => 2,
                'passivePerception' => 13, 'inspiration' => true, 'languages' => 'Common, Orc',
                'features' => "Unarmored Defense\nAC 10 + Dex + Con\nDanger Sense\nAdvantage on Dexterity saves\nRage (2 per rest)\nSpeedy Recovery (bonus action / long rest)",
                'traits' => 'Stubborn, direct, and built for the long road.',
                'notes' => 'Keep an eye on the northern pass. The old road is moving again.',
            ],
            'scores' => ['str' => 16, 'dex' => 14, 'con' => 16, 'int' => 10, 'wis' => 12, 'cha' => 8],
            'saves' => ['str' => true, 'dex' => false, 'con' => true, 'int' => false, 'wis' => false, 'cha' => false],
            'trainedSkills' => ['Athletics', 'Perception', 'Survival'],
            'weapons' => [
                ['name' => 'Greataxe', 'attack' => '+6', 'damage' => '1d12 + 3 slashing', 'notes' => ''],
                ['name' => 'Handaxe', 'attack' => '+6', 'damage' => '1d6 + 3 slashing', 'notes' => '20 / 60'],
                ['name' => 'Javelin', 'attack' => '+6', 'damage' => '1d6 + 3 piercing', 'notes' => '30 / 120'],
                ['name' => 'Heavy Crossbow', 'attack' => '+5', 'damage' => '1d10 + 2 piercing', 'notes' => '100 / 400, push'],
            ],
            'spellSlots' => array_fill(0, 9, 0),
        ];
    }
}