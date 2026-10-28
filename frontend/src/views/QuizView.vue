<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ApiError, quizApi, type QuizProgress } from '../api/quizApi'
import QuestionForm from '../components/QuestionForm.vue'
import QuizSummary from '../components/QuizSummary.vue'

const props = defineProps<{
  quizId: string
  token?: string
}>()

const router = useRouter()

const progress = ref<QuizProgress | null>(null)
const loading = ref(false)
const loadError = ref<string | null>(null)
const loadErrorIsNotFound = ref(false)
const submitting = ref(false)
const submitError = ref<string | null>(null)
const notice = ref<string | null>(null)

// Guards against out-of-order responses when the route changes while a request is in flight.
let loadSequence = 0

async function load() {
  const sequence = ++loadSequence
  loading.value = true
  loadError.value = null
  loadErrorIsNotFound.value = false
  try {
    const result = await quizApi.getProgress(props.quizId, props.token ?? null)
    if (sequence === loadSequence) progress.value = result
  } catch (error) {
    if (sequence === loadSequence) {
      progress.value = null
      loadError.value = errorMessage(error)
      loadErrorIsNotFound.value = error instanceof ApiError && error.status === 404
    }
  } finally {
    if (sequence === loadSequence) loading.value = false
  }
}

watch(
  () => [props.quizId, props.token] as const,
  ([quizId, token]) => {
    // Adding the token to the URL after the first answer shouldn't trigger a reload; we already have that state.
    if (progress.value?.quizId === quizId && (progress.value.token ?? undefined) === token) return
    void load()
  },
  { immediate: true },
)

async function submitAnswer(choiceId: string) {
  const question = progress.value?.currentQuestion
  if (!question || submitting.value) return

  submitting.value = true
  submitError.value = null
  notice.value = null
  try {
    const result = await quizApi.answer(props.quizId, props.token ?? null, question.id, choiceId)
    progress.value = result

    // Replace rather than push so "Back" can't return to an already-answered question.
    if (result.token && result.token !== props.token) {
      await router.replace({ name: 'quiz', params: { quizId: props.quizId, token: result.token } })
    }
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      // The attempt moved on elsewhere (e.g. another tab); resync with the server's view.
      notice.value = 'That question was already answered. Continuing from where you left off.'
      await load()
    } else {
      submitError.value = errorMessage(error)
    }
  } finally {
    submitting.value = false
  }
}

function errorMessage(error: unknown): string {
  return error instanceof ApiError ? error.message : 'Something went wrong. Please check your connection and try again.'
}
</script>

<template>
  <div class="card">
    <p v-if="loading && !progress">Loading…</p>

    <div v-else-if="loadError" role="alert">
      <p class="error">{{ loadError }}</p>
      <button v-if="!loadErrorIsNotFound" type="button" @click="load">Try again</button>
      <p v-else-if="token"><RouterLink :to="{ name: 'quiz', params: { quizId } }">Start this quiz over</RouterLink></p>
    </div>

    <template v-else-if="progress">
      <h1>{{ progress.title }}</h1>

      <p v-if="notice" role="status">{{ notice }}</p>

      <QuestionForm
        v-if="progress.currentQuestion"
        :key="progress.currentQuestion.id"
        :question="progress.currentQuestion"
        :total-questions="progress.totalQuestions"
        :submitting="submitting"
        @submit="submitAnswer"
      />
      <QuizSummary v-else :items="progress.summary ?? []" />

      <p v-if="submitError" class="error" role="alert">{{ submitError }}</p>
    </template>
  </div>
</template>
