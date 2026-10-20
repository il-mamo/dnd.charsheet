<?php

use App\Http\Controllers\CharacterSheetController;
use Illuminate\Support\Facades\Route;

Route::get('/', [CharacterSheetController::class, 'show'])->name('home');
Route::put('/character-sheet', [CharacterSheetController::class, 'update'])->name('character-sheet.update');
