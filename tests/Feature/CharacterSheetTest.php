<?php

namespace Tests\Feature;

use App\Models\CharacterSheet;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class CharacterSheetTest extends TestCase
{
    use RefreshDatabase;

    public function test_the_default_sheet_is_loaded_from_the_database(): void
    {
        $response = $this->get(route('home'));

        $response->assertOk();
        $this->assertSame('Justyn', CharacterSheet::query()->firstOrFail()->data['character']['name']);
    }

    public function test_the_sheet_can_be_saved_to_the_database(): void
    {
        $payload = [
            'character' => ['name' => 'Mira'],
            'scores' => ['str' => 18],
            'saves' => ['str' => true],
            'trainedSkills' => ['Athletics'],
            'weapons' => [['name' => 'Longsword']],
            'spellSlots' => [1, 0, 0],
        ];

        $response = $this->put(route('character-sheet.update'), $payload);

        $response->assertRedirect();
        $this->assertSame($payload, CharacterSheet::query()->firstOrFail()->data);
    }
}