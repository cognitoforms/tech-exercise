<script setup lang="ts">
import { ref } from 'vue'
import type { QuestionView } from '../api/quizApi'

const props = defineProps<{
  question: QuestionView
  totalQuestions: number
  submitting: boolean
}>()

const emit = defineEmits<{ submit: [choiceId: string] }>()

// Parent keys this component by question id, so selection resets naturally for each new question.
const selectedChoiceId = ref<string | null>(null)

function submit() {
  if (selectedChoiceId.value && !props.submitting) emit('submit', selectedChoiceId.value)
}
</script>

<template>
  <form class="question" @submit.prevent="submit">
    <fieldset :disabled="submitting">
      <legend>
        <span class="question__progress">Question {{ question.number }} of {{ totalQuestions }}</span>
        <span class="question__text">{{ question.text }}</span>
      </legend>

      <label v-for="choice in question.choices" :key="choice.id" class="question__choice">
        <input v-model="selectedChoiceId" type="radio" :name="question.id" :value="choice.id" />
        {{ choice.text }}
      </label>
    </fieldset>

    <button type="submit" :disabled="!selectedChoiceId || submitting">
      {{ question.number === totalQuestions ? 'Finish' : 'Next' }}
    </button>
  </form>
</template>

<style scoped>
fieldset {
  border: 0;
  padding: 0;
  margin: 0 0 1.5rem;
}

legend {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.question__progress {
  font-size: 0.875rem;
  color: #616e7c;
}

.question__text {
  font-size: 1.25rem;
  font-weight: 600;
}

.question__choice {
  display: block;
  padding: 0.5rem 0;
  cursor: pointer;
}
</style>
