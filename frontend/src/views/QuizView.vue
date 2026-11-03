<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ApiError, quizApi, type QuizProgress } from '../api/quizApi'
import QuestionForm from '../components/QuestionForm.vue'
import QuizSummary from '../components/QuizSummary.vue'
import { quizTimeLimits } from '../quizTimeLimits'

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

const timeLimitSeconds = computed(() => quizTimeLimits[props.quizId] ?? null)
const startedAt = ref<number | null>(null)
const now = ref(Date.now())
const secondsLeft = computed(() => {
  if (startedAt.value === null || timeLimitSeconds.value === null) return null
  return Math.max(0, Math.ceil((startedAt.value + timeLimitSeconds.value * 1000 - now.value) / 1000))
})
const timedOut = computed(() => secondsLeft.value === 0)

const startKey = (token: string) => `quiz-started:${token}`

function readStartedAt(token: string | undefined): number | null {
  if (!token || timeLimitSeconds.value === null) return null
  const stored = Number(localStorage.getItem(startKey(token)))
  return Number.isFinite(stored) && stored > 0 ? stored : null
}

let timerId: ReturnType<typeof setInterval> | undefined

watch(
  () => (secondsLeft.value ?? 0) > 0,
  (running) => {
    clearInterval(timerId)
    timerId = undefined
    if (running) timerId = setInterval(() => (now.value = Date.now()), 250)
  },
  { immediate: true },
)

onUnmounted(() => clearInterval(timerId))

function formatTime(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = String(totalSeconds % 60).padStart(2, '0')
  return `${minutes}:${seconds}`
}

// Guards against out-of-order responses when the route changes while a request is in flight.
let loadSequence = 0

async function load() {
  const sequence = ++loadSequence
  loading.value = true
  loadError.value = null
  loadErrorIsNotFound.value = false
  try {
    const result = await quizApi.getProgress(props.quizId, props.token ?? null)
    if (sequence === loadSequence) {
      progress.value = result
      startedAt.value = readStartedAt(props.token)
    }
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
  if (!question || submitting.value || timedOut.value) return

  submitting.value = true
  submitError.value = null
  notice.value = null
  try {
    const isFirstAnswer = props.token === undefined
    const result = await quizApi.answer(props.quizId, props.token ?? null, question.id, choiceId)
    progress.value = result

    // The clock starts with the first answer.
    if (isFirstAnswer && result.token && timeLimitSeconds.value !== null) {
      localStorage.setItem(startKey(result.token), String(Date.now()))
      startedAt.value = Date.now()
    }

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

      <p v-if="secondsLeft !== null && progress.currentQuestion && !timedOut" class="timer" role="timer">
        Time left: {{ formatTime(secondsLeft) }}
      </p>
      <p v-if="timedOut && progress.currentQuestion" class="error" role="alert">
        Time's up. Unanswered questions were left blank.
      </p>

      <p v-if="notice" role="status">{{ notice }}</p>

      <QuestionForm
        v-if="progress.currentQuestion"
        :key="progress.currentQuestion.id"
        :question="progress.currentQuestion"
        :total-questions="progress.totalQuestions"
        :submitting="submitting || timedOut"
        @submit="submitAnswer"
      />
      <QuizSummary v-else :items="progress.summary ?? []" />

      <p v-if="submitError" class="error" role="alert">{{ submitError }}</p>
    </template>
  </div>
</template>
