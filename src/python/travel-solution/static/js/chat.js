document.addEventListener('DOMContentLoaded', () => {
  const chatId = window.chatId;
  const messagesEl = document.getElementById('messages');
  const form = document.getElementById('chatForm');
  const textarea = document.getElementById('message');

  function appendMessage(text, role) {
    const bubble = document.createElement('div');
    bubble.className = 'bubble ' + role;
    const html = role === 'assistant' ? marked.parse(text) : text.replace(/</g, '&lt;');
    bubble.innerHTML = html;
    messagesEl.appendChild(bubble);
    window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
  }

  async function submitMessage() {
    const content = textarea.value.trim();
    if (!content) return;
    appendMessage(content, 'user');
    textarea.value = '';

    try {
      const res = await fetch(`/api/chats/${chatId}/messages`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: content })
      });

      if (!res.ok) {
        appendMessage('Sorry, something went wrong.', 'assistant');
        return;
      }

      const data = await res.json();
      appendMessage(data.markdown, 'assistant');
    } catch (err) {
      appendMessage('Network error. Please try again.', 'assistant');
      console.error(err);
    }
  }

  if (form) {
    form.addEventListener('submit', async (e) => {
      e.preventDefault();
      await submitMessage();
    });

    // Allow Enter to send; Shift+Enter inserts newline
    textarea.addEventListener('keydown', async (e) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        await submitMessage();
      }
    });
  }
});
