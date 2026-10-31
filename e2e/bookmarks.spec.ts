import { test, expect, type Page } from '@playwright/test'

// Removes the test's own bookmark so the shared database keeps only the seed data.
async function deleteBookmark(page: Page, title: string) {
  await page.getByTestId('bookmark-item').filter({ hasText: title }).getByRole('button', { name: 'Delete' }).click()
  await expect(page.getByTestId('bookmark-item').filter({ hasText: title })).toHaveCount(0)
}

test('list shows seeded bookmarks with tags on load', async ({ page }) => {
  await page.goto('/')

  await expect(page.getByTestId('bookmark-item').first()).toBeVisible()
  await expect(page.getByTestId('tag-chip').first()).toBeVisible()
})

test('adding a bookmark shows it in the list', async ({ page }) => {
  const title = `E2E bookmark ${Date.now()}`
  await page.goto('/')

  const form = page.getByTestId('add-form')
  await form.getByLabel('Title', { exact: true }).fill(title)
  await form.getByLabel('URL', { exact: true }).fill('https://example.com/e2e')
  await form.getByLabel('Tags', { exact: true }).fill('e2e, testing')
  await form.getByRole('button', { name: 'Add bookmark' }).click()

  const item = page.getByTestId('bookmark-item').filter({ hasText: title })
  await expect(item).toBeVisible()
  await expect(item.getByText('e2e', { exact: true })).toBeVisible()

  await deleteBookmark(page, title)
})

test('an invalid URL shows the validation message from the API', async ({ page }) => {
  await page.goto('/')

  const form = page.getByTestId('add-form')
  await form.getByLabel('Title', { exact: true }).fill('Not a link')
  await form.getByLabel('URL', { exact: true }).fill('javascript:alert(1)')
  await form.getByRole('button', { name: 'Add bookmark' }).click()

  await expect(form.getByText(/valid URL starting with http/)).toBeVisible()
})

test('search narrows the list to matching bookmarks', async ({ page }) => {
  const title = `Searchable ${Date.now()}`
  await page.goto('/')

  const form = page.getByTestId('add-form')
  await form.getByLabel('Title', { exact: true }).fill(title)
  await form.getByLabel('URL', { exact: true }).fill('https://example.com/search')
  await form.getByRole('button', { name: 'Add bookmark' }).click()
  await expect(page.getByTestId('bookmark-item').filter({ hasText: title })).toBeVisible()

  await page.getByTestId('search-input').fill(title)

  await expect(page.getByTestId('bookmark-item')).toHaveCount(1)
  await expect(page.getByTestId('bookmark-item')).toContainText(title)

  await deleteBookmark(page, title)
})

test('selecting a tag chip filters the list to that tag', async ({ page }) => {
  await page.goto('/')

  const chip = page.getByTestId('tag-chip').filter({ hasText: '#docs' })
  await chip.click()

  await expect(chip).toHaveAttribute('aria-pressed', 'true')
  const items = page.getByTestId('bookmark-item')
  await expect(items.first()).toBeVisible()
  // Retrying assertion: waits until the filtered results replace the unfiltered ones.
  await expect(items.filter({ hasNot: page.getByText('docs', { exact: true }) })).toHaveCount(0)
})
