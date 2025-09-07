window.downloadText = (filename, content, mimeType) => {
  try {
    const type = mimeType || 'text/plain;charset=utf-8';
    const blob = new Blob([content], { type });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(() => URL.revokeObjectURL(link.href), 1000);
  } catch (e) {
    console.error('downloadText failed', e);
  }
};

window.downloadBase64 = (filename, base64, mimeType) => {
  try {
    const link = document.createElement('a');
    link.download = filename;
    link.href = `data:${mimeType};base64,${base64}`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  } catch (e) {
    console.error('downloadBase64 failed', e);
  }
};
