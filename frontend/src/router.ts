import { createRouter, createWebHistory } from 'vue-router'
import QuizView from './views/QuizView.vue'
import NotFoundView from './views/NotFoundView.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      // `token` is added once the first question is answered, making the URL resumable.
      path: '/quiz/:quizId/:token?',
      name: 'quiz',
      component: QuizView,
      props: true,
    },
    { path: '/:pathMatch(.*)*', component: NotFoundView },
  ],
})
