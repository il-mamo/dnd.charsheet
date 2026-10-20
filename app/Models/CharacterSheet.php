<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;

class CharacterSheet extends Model
{
    protected $fillable = ['data'];

    protected function casts(): array
    {
        return ['data' => 'array'];
    }
}