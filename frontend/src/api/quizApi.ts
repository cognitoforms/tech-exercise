export interface ChoiceView {
  id: string
  text: string
}

export interface QuestionView {
  id: string
  number: number
  text: string
  choices: ChoiceView[]
}

export interface SummaryItem {
  number: number
  questionId: string
  questionText: string
  answerText: string
}

export interface QuizProgress {
  quizId: string
  title: string
  /** Resume token; null until the first question has been answered. */
  token: string | null
  totalQuestions: number
  answeredCount: number
  /** The next question to answer, or null once the quiz is complete. */
  currentQuestion: QuestionView | null
  /** Present only once every question has been answered (or time ran out). */
  summary: SummaryItem[] | null
  isComplete: boolean
  /** Whole seconds left on a timed quiz; null when untimed or complete. */
  timeRemainingSeconds: number | null
  /** True when the time limit passed before every question was answered. */
  timedOut: boolean
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', ...init?.headers },
  })

  if (!response.ok) {
    // The API returns RFC 7807 problem details; fall back to a generic message if the body isn't one.
    const problem = await response.json().catch(() => null)
    throw new ApiError(response.status, problem?.title ?? `Request failed (${response.status}).`)
  }

  return (await response.json()) as T
}

const quizUrl = (quizId: string) => `/api/quizzes/${encodeURIComponent(quizId)}`
const attemptUrl = (quizId: string, token: string) => `${quizUrl(quizId)}/attempts/${encodeURIComponent(token)}`

export const quizApi = {
  /** Loads a quiz (when `token` is null) or resumes an attempt at its first unanswered question. */
  getProgress(quizId: string, token: string | null): Promise<QuizProgress> {
    return request(token ? attemptUrl(quizId, token) : quizUrl(quizId))
  },

  /** Answers the current question. Without a token this starts a new attempt and the response includes its token. */
  answer(quizId: string, token: string | null, questionId: string, choiceId: string): Promise<QuizProgress> {
    const url = token ? `${attemptUrl(quizId, token)}/answers` : `${quizUrl(quizId)}/attempts`
    return request(url, { method: 'POST', body: JSON.stringify({ questionId, choiceId }) })
  },
}
