window.addEventListener('scroll', () => {
    const header = document.querySelector('header');
    if (window.scrollY > 50) {
        header.classList.add('scrolled');
    } else {
        header.classList.remove('scrolled');
    }
});

const mobileMenuBtn = document.getElementById('mobileMenuBtn');
if (mobileMenuBtn) {
    mobileMenuBtn.addEventListener('click', () => {
        console.log('Mobile menu clicked');
    });
}

const contactModal = document.getElementById('contactModal');
const contactLink = document.getElementById('contactLink');
const closeBtn = document.querySelector('.close-btn');
const contactForm = document.getElementById('contactForm');
const formStatus = document.getElementById('formStatus');

if (contactLink && contactModal) {
    contactLink.addEventListener('click', (e) => {
        e.preventDefault();
        contactModal.classList.add('active');
        document.body.style.overflow = 'hidden';
    });
}

if (closeBtn && contactModal) {
    closeBtn.addEventListener('click', () => {
        contactModal.classList.remove('active');
        document.body.style.overflow = 'auto';
    });

    window.addEventListener('click', (e) => {
        if (e.target === contactModal) {
            contactModal.classList.remove('active');
            document.body.style.overflow = 'auto';
        }
    });
}

if (contactForm) {
    contactForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        const name = document.getElementById('name').value;
        const discord = document.getElementById('discord').value;
        const message = document.getElementById('message').value;
        const submitBtn = document.getElementById('submitBtn');

        submitBtn.disabled = true;
        submitBtn.textContent = 'Sending...';
        formStatus.textContent = '';
        formStatus.className = 'form-status';

        const webhookUrl = atob('aHR0cHM6Ly9kaXNjb3JkLmNvbS9hcGkvd2ViaG9va3MvMTQ3MDk1NDE4MDY2NjU5MzUyNC9UUVdhUldzQnA1dVdaZmszOFJrSFJDQ0tMLWxrTTc5YnlBYUhGTDN3cDdwNE9rRmxYY2x1eGoyWkNSVWRSYmM2UzJ5cQ==');

        if (!webhookUrl) {
            formStatus.textContent = 'Error: Webhook URL not configured.';
            formStatus.classList.add('error');
            submitBtn.disabled = false;
            submitBtn.textContent = 'Send Message';
            return;
        }

        const payload = {
            embeds: [{
                title: "New Contact Message",
                color: 5814783,
                fields: [
                    { name: "Name", value: name, inline: true },
                    { name: "Contact", value: discord, inline: true },
                    { name: "Message", value: message }
                ],
                footer: { text: "Sent from JustLauncher Website" },
                timestamp: new Date().toISOString()
            }]
        };

        try {
            const response = await fetch(webhookUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            if (response.ok) {
                formStatus.textContent = 'Message sent successfully!';
                formStatus.classList.add('success');
                contactForm.reset();
                setTimeout(() => {
                    contactModal.classList.remove('active');
                    document.body.style.overflow = 'auto';
                    formStatus.textContent = '';
                }, 2000);
            } else {
                const errorData = await response.json().catch(() => ({}));
                console.error('Discord response error:', response.status, errorData);
                throw new Error(`Failed to send message (Status: ${response.status})`);
            }
        } catch (error) {
            console.error('Webhook error:', error);
            formStatus.textContent = 'Failed to send message. Please try again later.';
            formStatus.classList.add('error');
        } finally {
            submitBtn.disabled = false;
            submitBtn.textContent = 'Send Message';
        }
    });
}

const observerOptions = {
    threshold: 0.1
};

const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
        if (entry.isIntersecting) {
            entry.target.classList.add('animate-up');
            observer.unobserve(entry.target);
        }
    });
}, observerOptions);

fetch('https://api.github.com/repos/PinkLittleKitty/JustLauncher/releases/latest')
    .then(response => response.json())
    .then(data => {
        const winBtn = document.getElementById('download-win');
        const linuxBtn = document.getElementById('download-linux');
        const heroBtn = document.getElementById('hero-download');

        let winUrl = 'https://github.com/PinkLittleKitty/JustLauncher/releases/latest';
        let linuxUrl = 'https://github.com/PinkLittleKitty/JustLauncher/releases/latest';

        if (data.assets) {
            data.assets.forEach(asset => {
                if (asset.name.includes('win64.exe')) {
                    winUrl = asset.browser_download_url;
                    if (winBtn) winBtn.href = winUrl;
                }
                if (asset.name.includes('linux') && !asset.name.includes('exe')) {
                    linuxUrl = asset.browser_download_url;
                    if (linuxBtn) linuxBtn.href = linuxUrl;
                }
            });

            const platform = window.navigator.platform.toLowerCase();
            const userAgent = window.navigator.userAgent.toLowerCase();

            if (heroBtn) {
                if (platform.includes('win') || userAgent.includes('windows')) {
                    heroBtn.href = winUrl;
                    heroBtn.innerHTML = '<i class="fab fa-windows"></i> Download for Windows';
                } else if (platform.includes('linux') || userAgent.includes('linux')) {
                    heroBtn.href = linuxUrl;
                    heroBtn.innerHTML = '<i class="fab fa-linux"></i> Download for Linux';
                } else {
                    heroBtn.href = '#download';
                }
            }

            console.log(`Updated download links to ${data.tag_name}`);
        }
    })
    .catch(error => {
        console.error('Error fetching latest release:', error);
        const heroBtn = document.getElementById('hero-download');
        if (heroBtn) heroBtn.href = '#download';
    });

document.querySelectorAll('.feature-card, .section-header, .download .container').forEach(el => {
    observer.observe(el);
});

document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function (e) {
        const href = this.getAttribute('href');
        if (href.startsWith('#')) {
            if (href === '#') return;
            e.preventDefault();
            const target = document.querySelector(href);
            if (target) {
                window.scrollTo({
                    top: target.offsetTop - 80,
                    behavior: 'smooth'
                });
            }
        }
    });
});
