// Album of the Day: adds a first entry to the music library view menu (before "Albums")
// and renders the day's pick in place of the library grid. Targets jellyfin-web 12.2.
(function () {
    'use strict';

    const LABEL = 'Album of the Day';
    const VIEW_BUTTON = 'button[aria-controls="library-view-menu"]';
    let active = false;
    let lastUrl = null;
    let originalTitle = null;

    const style = document.createElement('style');
    style.textContent = `
        #musicPage.aotd-active .padded-bottom-page > :not(.aotd-panel) { display: none !important; }
        .MuiToolbar-root.aotd-active > :not(${VIEW_BUTTON}) { display: none !important; }
        .aotd-panel { display: flex; flex-wrap: wrap; justify-content: center; align-items: center; gap: 2em; padding-top: 2em; }
        .aotd-panel > a { flex: 0 0 min(100%, 360px); color: inherit; }
        .aotd-art { display: flex; align-items: center; justify-content: center; width: 100%; aspect-ratio: 1;
            object-fit: cover; border-radius: .3em; background: rgba(127, 127, 127, .2); font-size: 8em; }
        .aotd-panel h2 { margin: 0 0 .3em; }
        .aotd-panel p { margin: 0 0 .3em; opacity: .8; }
        .aotd-actions { display: flex; align-items: center; gap: 1em; margin-top: 1em; }
        .aotd-actions .button-submit { display: inline-flex; align-items: center; gap: .3em; margin: 0; }
        .aotd-previous { flex: 0 0 100%; margin-top: 1em; }
        .aotd-previous-list { display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 1.2em; }
        .aotd-previous-list a { display: flex; flex-direction: column; gap: .2em; color: inherit; text-decoration: none; min-width: 0; }
        .aotd-previous-list .aotd-art { font-size: 4em; margin-bottom: .1em; }
        .aotd-previous-list span:not(.aotd-art) { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: .9em; }
        .aotd-previous-list span:not(.aotd-art, :first-of-type) { opacity: .7; }`;
    document.head.appendChild(style);

    function libraryId() {
        const m = location.hash.match(/^#\/music\?(.*)$/);
        return m && new URLSearchParams(m[1]).get('topParentId');
    }

    // Picking any built-in view adds &tab=N, so a library URL without it means the library was just opened.
    function isDefaultView() {
        return !!libraryId() && !new URLSearchParams(location.hash.split('?')[1]).has('tab');
    }

    function titleEl() {
        return document.querySelector(VIEW_BUTTON + ' .MuiTypography-root');
    }

    function setActive(on) {
        if (active === on) return;
        active = on;
        if (!on) {
            const title = titleEl();
            if (title && originalTitle && title.textContent === LABEL) title.textContent = originalTitle;
            originalTitle = null;
            document.querySelector('.aotd-panel')?.remove();
        }
        sync();
    }

    // Re-applies our state; called on every DOM mutation because React may re-render around us.
    function sync() {
        // The router navigates with pushState (no hashchange event), so URL changes are detected here.
        // Opening a music library shows the album of the day; any other navigation leaves it.
        if (location.href !== lastUrl) {
            lastUrl = location.href;
            setActive(false);
            setActive(isDefaultView());
            return;
        }

        const page = document.querySelector('#musicPage');
        const on = active && !!page && !!libraryId();
        page?.classList.toggle('aotd-active', on);
        document.querySelector(VIEW_BUTTON)?.closest('.MuiToolbar-root')?.classList.toggle('aotd-active', on);
        if (on && !page.querySelector('.aotd-panel')) render(page);
        // The title may render after activation, so it is set here rather than in setActive.
        const title = titleEl();
        if (on && title && title.textContent !== LABEL) {
            originalTitle = title.textContent;
            title.textContent = LABEL;
        }
        addMenuItem();
    }

    function addMenuItem() {
        const list = document.querySelector('#library-view-menu [role="menu"]');
        if (!list || !libraryId() || list.querySelector('.aotd-item')) return;

        const item = list.firstElementChild.cloneNode(false);
        item.classList.add('aotd-item');
        item.classList.toggle('Mui-selected', active);
        if (active) list.querySelectorAll('.Mui-selected').forEach(el => el.classList.remove('Mui-selected'));
        item.textContent = LABEL;
        item.addEventListener('click', () => {
            setActive(true);
            // MUI closes the menu on a backdrop click
            document.querySelector('#library-view-menu .MuiBackdrop-root')?.click();
        });
        list.prepend(item);
        list.addEventListener('click', e => {
            if (!e.target.closest('.aotd-item')) setActive(false);
        });
    }

    async function render(page) {
        const id = libraryId();
        const panel = document.createElement('div');
        panel.className = 'aotd-panel padded-left padded-right';
        (page.querySelector('.padded-bottom-page') || page).prepend(panel);
        try {
            const { Id, Previous = [] } = await ApiClient.getJSON(ApiClient.getUrl('AlbumOfTheDay/' + id));
            const userId = ApiClient.getCurrentUserId();
            const [album, previous] = await Promise.all([
                ApiClient.getItem(userId, Id),
                Previous.length ? ApiClient.getItems(userId, { Ids: Previous.map(p => p.Id).join(',') }) : { Items: [] }
            ]);
            if (id !== libraryId()) return;

            const link = document.createElement('a');
            link.href = detailsUrl(album);
            link.appendChild(albumArt(album, 720));

            const info = document.createElement('div');
            const name = document.createElement('h2');
            name.textContent = album.Name;
            info.appendChild(name);
            for (const text of [album.AlbumArtist, album.ProductionYear]) {
                if (!text) continue;
                const p = document.createElement('p');
                p.textContent = text;
                info.appendChild(p);
            }
            // Jellyfin's item shortcuts (the same handler card play buttons use) start playback for
            // [data-action="play"] inside an emby-itemscontainer, using the data-* item info around it.
            info.insertAdjacentHTML('beforeend', `
                <div is="emby-itemscontainer" class="aotd-actions">
                    <button is="emby-button" type="button" class="raised button-submit itemAction" data-action="play">
                        <span class="material-icons play_arrow" aria-hidden="true"></span><span>Play</span>
                    </button>
                </div>`);
            const actions = info.lastElementChild;
            Object.assign(actions.dataset, { id: album.Id, serverid: album.ServerId, type: album.Type, isfolder: 'true' });
            const open = link.cloneNode(false);
            open.className = 'button-link';
            open.textContent = 'Open album';
            actions.appendChild(open);

            panel.replaceChildren(link, info);
            if (previous.Items.length) panel.appendChild(previousSection(Previous, previous.Items));
        } catch {
            panel.textContent = 'No album of the day for this library yet.';
        }
    }

    function detailsUrl(album) {
        return `#/details?id=${album.Id}&serverId=${album.ServerId}`;
    }

    function albumArt(album, maxHeight) {
        const tag = album.ImageTags?.Primary;
        const art = document.createElement(tag ? 'img' : 'span');
        if (tag) {
            art.alt = album.Name;
            art.src = ApiClient.getScaledImageUrl(album.Id, { type: 'Primary', maxHeight, tag });
        } else {
            art.className = 'material-icons album';
        }
        art.classList.add('aotd-art');
        return art;
    }

    // previous: [{ Id, Date }] newest first, as the API returns it; items: the same albums in any order.
    function previousSection(previous, items) {
        const byId = new Map(items.map(item => [item.Id, item]));
        const section = document.createElement('section');
        section.className = 'aotd-previous';
        const heading = document.createElement('h3');
        heading.textContent = 'Previous albums of the day';
        const list = document.createElement('div');
        list.className = 'aotd-previous-list';
        for (const { Id, Date: date } of previous) {
            const album = byId.get(Id);
            if (!album) continue;
            const card = document.createElement('a');
            card.href = detailsUrl(album);
            card.appendChild(albumArt(album, 300));
            const day = new Date(date + 'T00:00').toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
            for (const text of [album.Name, album.AlbumArtist, day]) {
                const span = document.createElement('span');
                span.textContent = text || '';
                card.appendChild(span);
            }
            list.appendChild(card);
        }
        section.append(heading, list);
        return section;
    }

    sync();
    new MutationObserver(sync).observe(document.body, { childList: true, subtree: true });
})();
