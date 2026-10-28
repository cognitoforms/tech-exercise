import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import QuestionForm from './QuestionForm.vue'
import type { QuestionView } from '../api/quizApi'

const question: QuestionView = {
  id: 'q1',
  number: 1,
  text: 'Pick one',
  choices: [
    { id: 'a', text: 'Alpha' },
    { id: 'b', text: 'Beta' },
  ],
}

function mountForm(props: Partial<{ question: QuestionView; totalQuestions: number; submitting: boolean }> = {}) {
  return mount(QuestionForm, { props: { question, totalQuestions: 3, submitting: false, ...props } })
}

describe('QuestionForm', () => {
  it('disables the button until an answer is selected', async () => {
    const wrapper = mountForm()
    const button = wrapper.get('button')

    expect(button.element.disabled).toBe(true)

    await wrapper.findAll('input[type="radio"]')[1]!.setValue()
    expect(button.element.disabled).toBe(false)
  })

  it('emits the selected choice on submit', async () => {
    const wrapper = mountForm()

    await wrapper.findAll('input[type="radio"]')[1]!.setValue()
    await wrapper.get('form').trigger('submit')

    expect(wrapper.emitted('submit')).toEqual([['b']])
  })

  it('does not emit without a selection', async () => {
    const wrapper = mountForm()

    await wrapper.get('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('does not emit while a submission is in flight', async () => {
    const wrapper = mountForm()
    await wrapper.findAll('input[type="radio"]')[0]!.setValue()
    await wrapper.setProps({ submitting: true })

    await wrapper.get('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('labels the last question with Finish', () => {
    expect(mountForm({ totalQuestions: 1 }).get('button').text()).toBe('Finish')
    expect(mountForm().get('button').text()).toBe('Next')
  })
})
