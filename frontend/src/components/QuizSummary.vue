<script setup lang="ts">
import type { SummaryItem } from '../api/quizApi'

defineProps<{ items: SummaryItem[] }>()
</script>

<template>
  <section class="summary">
    <h2>Your answers</h2>
    <ol>
      <li v-for="item in items" :key="item.questionId">
        <div class="summary__question">{{ item.questionText }}</div>
        <div class="summary__answer">{{ item.answerText }}</div>
      </li>
    </ol>
  </section>
</template>

<style scoped>
ol {
  padding-left: 1.25rem;
}

li {
  margin-bottom: 1rem;
}

.summary__question {
  font-weight: 600;
}

.summary__answer {
  color: #2563eb;
}
</style>
