<script setup lang="ts">
import { Head, router } from '@inertiajs/vue3';
import { computed, ref, watch } from 'vue';

type AbilityKey = 'str' | 'dex' | 'con' | 'int' | 'wis' | 'cha';

interface Weapon {
    name: string;
    attack: string;
    damage: string;
    notes: string;
}

interface Character {
    name: string;
    species: string;
    className: string;
    subclass: string;
    background: string;
    level: number;
    xp: number;
    currentHp: number;
    maxHp: number;
    tempHp: number;
    armorClass: number;
    speed: number;
    hitDice: string;
    spentHitDice: number;
    proficiency: number;
    initiative: number;
    passivePerception: number;
    inspiration: boolean;
    languages: string;
    features: string;
    traits: string;
    notes: string;
}

interface SheetData {
    character: Character;
    scores: Record<AbilityKey, number>;
    saves: Record<AbilityKey, boolean>;
    trainedSkills: string[];
    weapons: Weapon[];
    spellSlots: number[];
}

const props = defineProps<{ sheet: SheetData }>();

const abilities: { key: AbilityKey; label: string; value: number }[] = [
    { key: 'str', label: 'Strength', value: 16 },
    { key: 'dex', label: 'Dexterity', value: 14 },
    { key: 'con', label: 'Constitution', value: 16 },
    { key: 'int', label: 'Intelligence', value: 10 },
    { key: 'wis', label: 'Wisdom', value: 12 },
    { key: 'cha', label: 'Charisma', value: 8 },
];

const skillGroups = [
    { name: 'Strength', skills: ['Athletics'] },
    { name: 'Dexterity', skills: ['Acrobatics', 'Sleight of Hand', 'Stealth'] },
    {
        name: 'Intelligence',
        skills: ['Arcana', 'History', 'Investigation', 'Nature', 'Religion'],
    },
    {
        name: 'Wisdom',
        skills: [
            'Animal Handling',
            'Insight',
            'Medicine',
            'Perception',
            'Survival',
        ],
    },
    {
        name: 'Charisma',
        skills: ['Deception', 'Intimidation', 'Performance', 'Persuasion'],
    },
];

const character = ref({ ...props.sheet.character });

const scores = ref({ ...props.sheet.scores });
const saves = ref<Record<AbilityKey, boolean>>({
    ...props.sheet.saves,
});
const trainedSkills = ref<string[]>(props.sheet.trainedSkills);
const weapons = ref<Weapon[]>(props.sheet.weapons);
const spellSlots = ref(props.sheet.spellSlots);
const activeTab = ref('overview');
const savedMessage = ref('All changes saved');
const showMobileNav = ref(false);

const modifier = (value: number) => Math.floor((value - 10) / 2);
const signed = (value: number) => (value >= 0 ? `+${value}` : `${value}`);
const hpPercent = computed(() =>
    Math.min(
        100,
        Math.max(0, (character.value.currentHp / character.value.maxHp) * 100),
    ),
);
const abilityModifier = (key: AbilityKey) => modifier(scores.value[key]);
const saveModifier = (key: AbilityKey) =>
    abilityModifier(key) + (saves.value[key] ? character.value.proficiency : 0);
const skillModifier = (skill: string, ability: AbilityKey) =>
    abilityModifier(ability) +
    (trainedSkills.value.includes(skill) ? character.value.proficiency : 0);

function toggleSkill(skill: string) {
    trainedSkills.value = trainedSkills.value.includes(skill)
        ? trainedSkills.value.filter((trainedSkill) => trainedSkill !== skill)
        : [...trainedSkills.value, skill];
}

function changeHp(amount: number) {
    character.value.currentHp = Math.min(
        character.value.maxHp,
        Math.max(0, character.value.currentHp + amount),
    );
}

function addWeapon() {
    weapons.value.push({
        name: 'New weapon',
        attack: '+0',
        damage: '1d6',
        notes: '',
    });
}

function saveSheet() {
    router.put(
        '/character-sheet',
        {
            character: character.value,
            scores: scores.value,
            saves: saves.value,
            trainedSkills: trainedSkills.value,
            weapons: weapons.value,
            spellSlots: spellSlots.value,
        },
        { preserveState: true, preserveScroll: true },
    );
    savedMessage.value = 'Saved just now';
    window.setTimeout(() => (savedMessage.value = 'All changes saved'), 2200);
}

let saveTimeout: number | undefined;
watch(
    [character, scores, saves, trainedSkills, weapons, spellSlots],
    () => {
        window.clearTimeout(saveTimeout);
        saveTimeout = window.setTimeout(saveSheet, 500);
    },
    { deep: true },
);
</script>

<template>
    <Head title="Character sheet" />
    <div class="sheet-shell">
        <header class="topbar">
            <div class="brand-lockup">
                <span class="brand-mark">DS</span><span>Dungeon Session</span>
            </div>
            <button
                class="mobile-toggle"
                type="button"
                @click="showMobileNav = !showMobileNav"
            >
                Menu
            </button>
            <nav :class="['main-nav', { 'is-open': showMobileNav }]">
                <button
                    v-for="tab in ['overview', 'combat', 'spells', 'notes']"
                    :key="tab"
                    :class="{ active: activeTab === tab }"
                    type="button"
                    @click="
                        activeTab = tab;
                        showMobileNav = false;
                    "
                >
                    {{ tab }}
                </button>
            </nav>
            <div class="save-state">
                <span class="save-dot"></span>{{ savedMessage }}
                <button class="save-button" type="button" @click="saveSheet">
                    Save sheet
                </button>
            </div>
        </header>

        <main class="page-content">
            <section class="hero-row">
                <div>
                    <p class="eyebrow">Campaign / The Boys play DnD</p>
                    <h1>{{ character.name }}</h1>
                    <p class="hero-subtitle">
                        {{ character.species }} <span>/</span>
                        {{ character.className }} {{ character.level }}
                        <span>/</span> {{ character.background }}
                    </p>
                </div>
                <div class="session-status">
                    <span class="status-pulse"></span
                    ><span
                        ><strong>Session ready</strong
                        ><small>Last edited today</small></span
                    >
                </div>
            </section>

            <section v-if="activeTab === 'overview'" class="dashboard-grid">
                <div class="column column-main">
                    <section class="panel identity-panel">
                        <div class="panel-heading">
                            <div>
                                <span class="section-number">01</span>
                                <h2>Identity</h2>
                            </div>
                            <span class="panel-caption">Character details</span>
                        </div>
                        <div class="identity-fields">
                            <label
                                >Character name<input v-model="character.name"
                            /></label>
                            <label
                                >Species<input v-model="character.species"
                            /></label>
                            <label
                                >Class<input v-model="character.className"
                            /></label>
                            <label
                                >Subclass<input v-model="character.subclass"
                            /></label>
                            <label
                                >Background<input
                                    v-model="character.background"
                            /></label>
                            <label
                                >Level<input
                                    v-model.number="character.level"
                                    type="number"
                                    min="1"
                            /></label>
                        </div>
                    </section>

                    <section class="panel ability-panel">
                        <div class="panel-heading">
                            <div>
                                <span class="section-number">02</span>
                                <h2>Ability scores</h2>
                            </div>
                            <span class="panel-caption"
                                >Click a score to edit</span
                            >
                        </div>
                        <div class="ability-grid">
                            <article
                                v-for="ability in abilities"
                                :key="ability.key"
                                class="ability-card"
                            >
                                <span class="ability-label">{{
                                    ability.label
                                }}</span>
                                <input
                                    v-model.number="scores[ability.key]"
                                    class="ability-score"
                                    type="number"
                                    min="1"
                                    max="30"
                                    :aria-label="`${ability.label} score`"
                                />
                                <span class="ability-mod"
                                    >{{
                                        signed(abilityModifier(ability.key))
                                    }}
                                    modifier</span
                                >
                                <label class="save-toggle"
                                    ><input
                                        v-model="saves[ability.key]"
                                        type="checkbox"
                                    /><span></span> Save
                                    {{
                                        signed(saveModifier(ability.key))
                                    }}</label
                                >
                            </article>
                        </div>
                    </section>

                    <section class="panel skills-panel">
                        <div class="panel-heading">
                            <div>
                                <span class="section-number">03</span>
                                <h2>Skills & saves</h2>
                            </div>
                            <span class="panel-caption"
                                >Tap a skill to mark proficiency</span
                            >
                        </div>
                        <div class="skills-list">
                            <div
                                v-for="group in skillGroups"
                                :key="group.name"
                                class="skill-group"
                            >
                                <h3>{{ group.name }}</h3>
                                <button
                                    v-for="skill in group.skills"
                                    :key="skill"
                                    :class="[
                                        'skill-row',
                                        {
                                            proficient:
                                                trainedSkills.includes(skill),
                                        },
                                    ]"
                                    type="button"
                                    @click="toggleSkill(skill)"
                                >
                                    <span class="skill-check">{{
                                        trainedSkills.includes(skill)
                                            ? '●'
                                            : '○'
                                    }}</span
                                    ><span>{{ skill }}</span
                                    ><strong>{{
                                        signed(
                                            skillModifier(
                                                skill,
                                                group.name
                                                    .toLowerCase()
                                                    .slice(0, 3) as AbilityKey,
                                            ),
                                        )
                                    }}</strong>
                                </button>
                            </div>
                        </div>
                    </section>
                </div>

                <aside class="column column-side">
                    <section class="panel combat-panel">
                        <div class="panel-heading">
                            <div>
                                <span class="section-number">04</span>
                                <h2>Combat</h2>
                            </div>
                            <span class="panel-caption"
                                >Live session stats</span
                            >
                        </div>
                        <div class="combat-top">
                            <div
                                class="hp-ring"
                                :style="{ '--hp-progress': `${hpPercent}%` }"
                            >
                                <strong>{{ character.currentHp }}</strong
                                ><span>/ {{ character.maxHp }} HP</span>
                            </div>
                            <div class="combat-metrics">
                                <label
                                    >Armor class<input
                                        v-model.number="character.armorClass"
                                        type="number" /></label
                                ><label
                                    >Initiative<input
                                        v-model.number="character.initiative"
                                        type="number" /></label
                                ><label
                                    >Speed<input
                                        v-model.number="character.speed"
                                        type="number"
                                /></label>
                            </div>
                        </div>
                        <div class="hp-actions">
                            <button type="button" @click="changeHp(-1)">
                                - 1 HP</button
                            ><button type="button" @click="changeHp(1)">
                                + 1 HP</button
                            ><label
                                >Temp<input
                                    v-model.number="character.tempHp"
                                    type="number"
                                    min="0"
                            /></label>
                        </div>
                        <div class="combat-details">
                            <span
                                ><small>Hit dice</small
                                >{{ character.spentHitDice }} /
                                {{ character.hitDice }}</span
                            ><span
                                ><small>Passive perception</small
                                >{{ character.passivePerception }}</span
                            ><span
                                ><small>Inspiration</small
                                ><button
                                    class="inspiration"
                                    :class="{ active: character.inspiration }"
                                    type="button"
                                    @click="
                                        character.inspiration =
                                            !character.inspiration
                                    "
                                >
                                    {{
                                        character.inspiration
                                            ? 'Ready'
                                            : 'Spent'
                                    }}
                                </button></span
                            >
                        </div>
                    </section>

                    <section class="panel weapons-panel">
                        <div class="panel-heading">
                            <div>
                                <span class="section-number">05</span>
                                <h2>Weapons</h2>
                            </div>
                            <button
                                class="text-button"
                                type="button"
                                @click="addWeapon"
                            >
                                + Add weapon
                            </button>
                        </div>
                        <div class="weapon-table">
                            <div class="weapon-head">
                                <span>Name</span><span>Attack</span
                                ><span>Damage / notes</span>
                            </div>
                            <div
                                v-for="weapon in weapons"
                                :key="weapon.name + weapon.damage"
                                class="weapon-row"
                            >
                                <input
                                    v-model="weapon.name"
                                    aria-label="Weapon name"
                                /><input
                                    v-model="weapon.attack"
                                    aria-label="Attack bonus"
                                />
                                <div>
                                    <input
                                        v-model="weapon.damage"
                                        aria-label="Weapon damage"
                                    /><input
                                        v-model="weapon.notes"
                                        class="weapon-note"
                                        aria-label="Weapon notes"
                                        placeholder="Notes"
                                    />
                                </div>
                            </div>
                        </div>
                    </section>

                    <section class="panel currency-panel">
                        <div class="panel-heading">
                            <div>
                                <span class="section-number">06</span>
                                <h2>Carried</h2>
                            </div>
                            <span class="panel-caption">Coins & languages</span>
                        </div>
                        <div class="coin-grid">
                            <label>PP<input value="0" type="number" /></label
                            ><label>GP<input value="130" type="number" /></label
                            ><label>EP<input value="0" type="number" /></label
                            ><label>SP<input value="46" type="number" /></label
                            ><label>CP<input value="5" type="number" /></label>
                        </div>
                        <label class="full-field"
                            >Languages<input v-model="character.languages"
                        /></label>
                    </section>
                </aside>
            </section>

            <section v-else-if="activeTab === 'combat'" class="tab-layout">
                <section class="panel combat-focus">
                    <div class="panel-heading">
                        <div>
                            <span class="section-number">04</span>
                            <h2>Combat tracker</h2>
                        </div>
                    </div>
                    <div class="combat-focus-grid">
                        <div class="big-hp">
                            <span>Current hit points</span
                            ><strong>{{ character.currentHp }}</strong>
                            <div class="hp-bar">
                                <span
                                    :style="{ width: `${hpPercent}%` }"
                                ></span>
                            </div>
                            <div class="hp-actions">
                                <button type="button" @click="changeHp(-5)">
                                    - 5 HP</button
                                ><button type="button" @click="changeHp(5)">
                                    + 5 HP
                                </button>
                            </div>
                        </div>
                        <div class="combat-metrics large">
                            <label
                                >Max HP<input
                                    v-model.number="character.maxHp"
                                    type="number" /></label
                            ><label
                                >Temp HP<input
                                    v-model.number="character.tempHp"
                                    type="number" /></label
                            ><label
                                >Armor class<input
                                    v-model.number="character.armorClass"
                                    type="number" /></label
                            ><label
                                >Hit dice<input v-model="character.hitDice"
                            /></label>
                        </div>
                    </div>
                </section>
            </section>

            <section v-else-if="activeTab === 'spells'" class="tab-layout">
                <section class="panel spell-panel">
                    <div class="panel-heading">
                        <div>
                            <span class="section-number">07</span>
                            <h2>Spell slots</h2>
                        </div>
                        <span class="panel-caption"
                            >Mark slots as they are spent</span
                        >
                    </div>
                    <div class="spell-levels">
                        <div
                            v-for="(slot, index) in spellSlots"
                            :key="index"
                            class="spell-level"
                        >
                            <span>Level {{ index + 1 }}</span>
                            <div>
                                <button
                                    v-for="number in 4"
                                    :key="number"
                                    :class="{ used: number <= slot }"
                                    type="button"
                                    @click="
                                        spellSlots[index] =
                                            number === slot
                                                ? number - 1
                                                : number
                                    "
                                >
                                    {{ number }}
                                </button>
                            </div>
                        </div>
                    </div>
                    <label class="spell-field"
                        >Spellcasting ability<input
                            value="None / martial character" /></label
                    ><label class="spell-field"
                        >Spell notes<textarea
                            placeholder="Add prepared spells, rituals, or session reminders..."
                        ></textarea>
                    </label>
                </section>
            </section>

            <section v-else class="tab-layout notes-layout">
                <section class="panel notes-panel">
                    <div class="panel-heading">
                        <div>
                            <span class="section-number">08</span>
                            <h2>Features & notes</h2>
                        </div>
                        <span class="panel-caption"
                            >Keep the table context close</span
                        >
                    </div>
                    <div class="notes-columns">
                        <label
                            >Class features<textarea
                                v-model="character.features"
                            ></textarea></label
                        ><label
                            >Species traits<textarea
                                v-model="character.traits"
                            ></textarea></label
                        ><label
                            >Backstory / session notes<textarea
                                v-model="character.notes"
                            ></textarea>
                        </label>
                    </div>
                </section>
            </section>

            <footer class="sheet-footer">
                <span>Dungeon Session / 5e character manager</span
                ><span>Autosave enabled</span>
            </footer>
        </main>
    </div>
</template>

<style scoped>
:global(body) {
    background: #f0f2ee;
}
.sheet-shell {
    min-height: 100vh;
    background:
        radial-gradient(circle at 86% 8%, #e4e9df 0, transparent 30%), #f0f2ee;
    color: #1c2a2e;
}
.topbar {
    display: flex;
    align-items: center;
    gap: 34px;
    min-height: 68px;
    padding: 0 5vw;
    border-bottom: 1px solid #d6ddd5;
    background: rgba(240, 242, 238, 0.88);
}
.brand-lockup {
    display: flex;
    align-items: center;
    gap: 11px;
    color: #203338;
    font-size: 12px;
    font-weight: 800;
    letter-spacing: 0.04em;
    text-transform: uppercase;
    white-space: nowrap;
}
.brand-mark {
    display: grid;
    place-items: center;
    width: 28px;
    height: 28px;
    background: #c96443;
    color: #fff;
    font-family: 'DM Mono', monospace;
    font-size: 10px;
    letter-spacing: -0.08em;
}
.main-nav {
    display: flex;
    align-self: stretch;
    gap: 23px;
}
.main-nav button,
.mobile-toggle {
    border: 0;
    background: transparent;
    color: #7b8988;
    font-size: 11px;
    font-weight: 800;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    cursor: pointer;
}
.main-nav button {
    position: relative;
}
.main-nav button.active {
    color: #1c2a2e;
}
.main-nav button.active::after {
    position: absolute;
    right: 0;
    bottom: 0;
    left: 0;
    height: 2px;
    background: #c96443;
    content: '';
}
.save-state {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-left: auto;
    color: #71807c;
    font-family: 'DM Mono', monospace;
    font-size: 10px;
}
.save-dot,
.status-pulse {
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background: #81a68e;
    box-shadow: 0 0 0 4px #dce9dd;
}
.save-button,
.text-button {
    border: 0;
    color: #c15d3d;
    background: transparent;
    font-size: 11px;
    font-weight: 800;
    cursor: pointer;
}
.page-content {
    width: min(1240px, 90vw);
    margin: 0 auto;
    padding: 58px 0 24px;
}
.hero-row {
    display: flex;
    align-items: flex-end;
    justify-content: space-between;
    margin-bottom: 44px;
}
.eyebrow,
.section-number,
.panel-caption,
label,
.ability-label,
.skill-group h3,
.weapon-head,
.combat-details small,
.sheet-footer,
.spell-level > span {
    color: #84918d;
    font-family: 'DM Mono', monospace;
    font-size: 10px;
    letter-spacing: 0.08em;
    text-transform: uppercase;
}
.eyebrow {
    margin: 0 0 13px;
    color: #c96443;
}
h1,
h2,
h3,
p {
    margin: 0;
}
h1 {
    color: #1b3035;
    font-size: clamp(34px, 5vw, 64px);
    font-weight: 800;
    letter-spacing: -0.06em;
    line-height: 0.95;
}
.hero-subtitle {
    margin-top: 14px;
    color: #70807e;
    font-size: 13px;
    font-weight: 600;
}
.hero-subtitle span {
    margin: 0 8px;
    color: #c7cfca;
}
.session-status {
    display: flex;
    align-items: center;
    gap: 13px;
    padding: 10px 0 5px;
}
.session-status strong,
.session-status small {
    display: block;
}
.session-status strong {
    font-size: 12px;
}
.session-status small {
    margin-top: 3px;
    color: #87938f;
    font-family: 'DM Mono', monospace;
    font-size: 10px;
}
.dashboard-grid {
    display: grid;
    grid-template-columns: minmax(0, 1.45fr) minmax(340px, 0.8fr);
    gap: 18px;
    align-items: start;
}
.column {
    display: grid;
    gap: 18px;
}
.panel {
    border: 1px solid #d7dfd8;
    background: rgba(250, 251, 247, 0.78);
    box-shadow: 0 12px 30px rgba(48, 67, 60, 0.035);
}
.panel-heading {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 18px;
    padding: 21px 23px 18px;
    border-bottom: 1px solid #dce2db;
}
.panel-heading > div {
    display: flex;
    align-items: baseline;
    gap: 12px;
}
.section-number {
    color: #c96443;
    font-weight: 500;
}
h2 {
    color: #23393d;
    font-size: 18px;
    font-weight: 800;
    letter-spacing: -0.035em;
}
.panel-caption {
    padding-top: 4px;
    font-size: 9px;
    text-align: right;
}
.identity-fields {
    display: grid;
    grid-template-columns: 1.4fr 1fr 1fr;
    gap: 20px 18px;
    padding: 23px;
}
label {
    display: grid;
    gap: 8px;
    color: #71807c;
    font-weight: 500;
}
input,
textarea {
    width: 100%;
    box-sizing: border-box;
    border: 0;
    border-bottom: 1px solid #cdd7cf;
    outline: 0;
    background: transparent;
    color: #203338;
    font-family: 'Manrope', sans-serif;
    font-size: 13px;
    font-weight: 700;
    transition:
        border-color 0.18s ease,
        background 0.18s ease;
}
input {
    min-height: 26px;
    padding: 3px 0;
}
textarea {
    min-height: 140px;
    padding: 8px 0;
    resize: vertical;
    line-height: 1.6;
}
input:focus,
textarea:focus {
    border-color: #c96443;
    background: rgba(201, 100, 67, 0.035);
}
.ability-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    padding: 22px;
    gap: 10px;
}
.ability-card {
    min-height: 136px;
    padding: 14px;
    border: 1px solid #d7dfd8;
    background: #f6f8f3;
}
.ability-label {
    display: block;
    font-weight: 500;
}
.ability-score {
    display: block;
    width: 58px;
    margin: 13px 0 1px;
    border: 0;
    color: #223b3e;
    font-size: 30px;
    font-weight: 800;
    letter-spacing: -0.06em;
}
.ability-mod {
    display: block;
    color: #c96443;
    font-family: 'DM Mono', monospace;
    font-size: 11px;
}
.save-toggle {
    display: flex;
    align-items: center;
    gap: 6px;
    margin-top: 14px;
    color: #82908c;
    font-family: 'DM Mono', monospace;
    font-size: 9px;
    text-transform: uppercase;
}
.save-toggle input {
    display: none;
}
.save-toggle span {
    display: inline-block;
    width: 8px;
    height: 8px;
    border: 1px solid #aab8ae;
    border-radius: 50%;
}
.save-toggle input:checked + span {
    border-color: #c96443;
    background: #c96443;
    box-shadow: inset 0 0 0 2px #f6f8f3;
}
.skills-list {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 25px 30px;
    padding: 22px;
}
.skill-group h3 {
    margin-bottom: 10px;
    color: #9b6b55;
    font-weight: 500;
}
.skill-row {
    display: grid;
    grid-template-columns: 17px 1fr auto;
    align-items: center;
    width: 100%;
    padding: 5px 0;
    border: 0;
    border-bottom: 1px solid #e3e8e2;
    background: transparent;
    color: #405255;
    font-size: 12px;
    text-align: left;
    cursor: pointer;
}
.skill-row strong {
    color: #7c8c87;
    font-family: 'DM Mono', monospace;
    font-size: 10px;
    font-weight: 500;
}
.skill-check {
    color: #a5b1ab;
    font-size: 10px;
}
.skill-row.proficient .skill-check,
.skill-row.proficient strong {
    color: #c96443;
}
.combat-top {
    display: grid;
    grid-template-columns: 1fr 1fr;
    align-items: center;
    gap: 18px;
    padding: 25px 23px 14px;
}
.hp-ring {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    width: 120px;
    height: 120px;
    border-radius: 50%;
    background: conic-gradient(#c96443 var(--hp-progress), #e0e7de 0);
    position: relative;
}
.hp-ring::before {
    position: absolute;
    inset: 8px;
    border-radius: 50%;
    background: #f9faf6;
    content: '';
}
.hp-ring strong,
.hp-ring span {
    position: relative;
    z-index: 1;
}
.hp-ring strong {
    color: #203a3d;
    font-size: 30px;
    letter-spacing: -0.07em;
}
.hp-ring span {
    color: #84918d;
    font-family: 'DM Mono', monospace;
    font-size: 9px;
}
.combat-metrics {
    display: grid;
    gap: 14px;
}
.combat-metrics label {
    grid-template-columns: 1fr 45px;
    align-items: center;
    gap: 8px;
}
.combat-metrics input {
    text-align: right;
}
.hp-actions {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 14px 23px 18px;
    border-top: 1px solid #e0e6df;
    border-bottom: 1px solid #e0e6df;
}
.hp-actions button {
    padding: 7px 9px;
    border: 1px solid #cfd9d0;
    background: #f9faf6;
    color: #647572;
    font-family: 'DM Mono', monospace;
    font-size: 10px;
    cursor: pointer;
}
.hp-actions button:hover {
    border-color: #c96443;
    color: #c96443;
}
.hp-actions label {
    grid-template-columns: auto 45px;
    align-items: center;
    margin-left: auto;
    gap: 8px;
}
.hp-actions input {
    text-align: right;
}
.combat-details {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 12px;
    padding: 17px 23px 20px;
    color: #31494c;
    font-size: 12px;
    font-weight: 700;
}
.combat-details small {
    display: block;
    margin-bottom: 5px;
    font-size: 8px;
}
.inspiration {
    padding: 0;
    border: 0;
    background: none;
    color: #a3ada8;
    font-size: 12px;
    font-weight: 700;
    cursor: pointer;
}
.inspiration.active {
    color: #c96443;
}
.weapon-table {
    padding: 0 23px 20px;
}
.weapon-head,
.weapon-row {
    display: grid;
    grid-template-columns: 1.1fr 0.55fr 1.7fr;
    align-items: center;
    gap: 12px;
}
.weapon-head {
    padding: 14px 0 9px;
    border-bottom: 1px solid #cfd9d0;
    font-size: 8px;
}
.weapon-row {
    min-height: 49px;
    border-bottom: 1px solid #e3e8e2;
}
.weapon-row input {
    font-size: 11px;
}
.weapon-row > input:nth-child(2) {
    color: #c96443;
}
.weapon-note {
    margin-top: 3px;
    color: #84918d;
    font-size: 9px !important;
}
.currency-panel {
    padding-bottom: 20px;
}
.coin-grid {
    display: grid;
    grid-template-columns: repeat(5, 1fr);
    gap: 11px;
    padding: 20px 23px 18px;
}
.coin-grid label {
    gap: 5px;
    font-size: 8px;
}
.coin-grid input {
    font-family: 'DM Mono', monospace;
    font-size: 11px;
}
.full-field {
    margin: 0 23px;
}
.tab-layout {
    display: block;
}
.combat-focus,
.spell-panel,
.notes-panel {
    min-height: 410px;
}
.combat-focus-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 60px;
    padding: 40px;
}
.big-hp > span {
    color: #84918d;
    font-family: 'DM Mono', monospace;
    font-size: 10px;
    text-transform: uppercase;
}
.big-hp strong {
    display: block;
    margin: 15px 0 17px;
    color: #c96443;
    font-size: 78px;
    letter-spacing: -0.08em;
    line-height: 1;
}
.hp-bar {
    height: 8px;
    background: #dde6dc;
}
.hp-bar span {
    display: block;
    height: 100%;
    background: #c96443;
    transition: width 0.2s;
}
.large {
    grid-template-columns: 1fr 1fr;
    align-content: start;
    gap: 25px;
}
.spell-levels {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 1px;
    padding: 23px;
    background: #dce3dc;
}
.spell-level {
    min-height: 100px;
    padding: 18px;
    background: #f9faf6;
}
.spell-level > span {
    display: block;
    margin-bottom: 22px;
}
.spell-level button {
    width: 29px;
    height: 29px;
    margin-right: 5px;
    border: 1px solid #cad6cc;
    border-radius: 50%;
    background: transparent;
    color: #87958e;
    font-family: 'DM Mono', monospace;
    font-size: 10px;
    cursor: pointer;
}
.spell-level button.used {
    border-color: #c96443;
    background: #c96443;
    color: #fff;
}
.spell-field {
    max-width: 400px;
    margin: 30px 23px 0;
}
.notes-columns {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 25px;
    padding: 25px 23px;
}
.notes-columns textarea {
    min-height: 250px;
}
.sheet-footer {
    display: flex;
    justify-content: space-between;
    padding-top: 31px;
    font-size: 9px;
}
.mobile-toggle {
    display: none;
}
@media (max-width: 850px) {
    .topbar {
        gap: 16px;
        padding: 0 5vw;
    }
    .main-nav {
        display: none;
        position: absolute;
        top: 68px;
        right: 5vw;
        left: 5vw;
        z-index: 5;
        height: auto;
        padding: 10px 15px;
        border: 1px solid #d7dfd8;
        background: #f9faf6;
        box-shadow: 0 12px 24px rgba(48, 67, 60, 0.08);
    }
    .main-nav.is-open,
    .mobile-toggle {
        display: flex;
    }
    .main-nav button {
        min-height: 38px;
        flex: 1;
    }
    .mobile-toggle {
        margin-left: auto;
    }
    .save-state {
        display: none;
    }
    .page-content {
        width: 90vw;
        padding-top: 42px;
    }
    .hero-row {
        align-items: flex-start;
        gap: 25px;
        flex-direction: column;
        margin-bottom: 30px;
    }
    .dashboard-grid {
        grid-template-columns: 1fr;
    }
}
@media (max-width: 560px) {
    .identity-fields,
    .ability-grid,
    .skills-list,
    .combat-top,
    .combat-focus-grid,
    .notes-columns {
        grid-template-columns: 1fr;
    }
    .ability-grid {
        gap: 8px;
    }
    .ability-card {
        min-height: 110px;
    }
    .ability-score {
        display: inline-block;
        margin-right: 10px;
    }
    .save-toggle {
        display: inline-flex;
        margin-top: 0;
    }
    .skills-list {
        gap: 20px;
    }
    .panel-heading {
        padding: 18px 16px;
    }
    .identity-fields,
    .ability-grid,
    .skills-list,
    .combat-top {
        padding: 18px 16px;
    }
    .weapon-table,
    .combat-details {
        padding-right: 16px;
        padding-left: 16px;
    }
    .coin-grid {
        padding-right: 16px;
        padding-left: 16px;
        gap: 7px;
    }
    .full-field {
        margin: 0 16px;
    }
    .spell-levels {
        grid-template-columns: repeat(2, 1fr);
        padding: 16px;
    }
    .combat-focus-grid,
    .notes-columns {
        gap: 30px;
        padding: 25px 16px;
    }
    .sheet-footer {
        gap: 15px;
        flex-direction: column;
    }
}
</style>
