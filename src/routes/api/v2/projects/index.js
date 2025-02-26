import { dbs } from '$lib/db/dbsetup';
import { jsonRequired, missingRequiredParam, authTokenRequired, notAllowed } from '$lib/utils/commonErrors';
import { retryOnServerError } from '$lib/utils/commonSqlHandlers';
import { verifyJwtAuth } from '$lib/utils/db/auth';
import { getAllProjects, countAllProjectsQuery, createOneProject } from '$lib/utils/db/projects';

export function get({ query }) {
    const db = query.private ? dbs.private : dbs.public;
    // URLSearchParams objects don't destructure well, so convert to a POJO
    const queryParams = Object.fromEntries(query);
    return getAllProjects(db, queryParams);
}

export async function head({ query }) {
    const db = query.private ? dbs.private : dbs.public;
    const queryParams = Object.fromEntries(query);
    const count = await retryOnServerError(countAllProjectsQuery(db, queryParams));
    const status = count > 0 ? 200 : 404;
    return { status, body: {} };
}

export async function post({ path, body, query, headers }) {
    if (typeof body !== 'object') {
        return jsonRequired('POST', path);
    }
    if (!body || !body.projectCode) {
        return missingRequiredParam('projectCode', `body of POST request to ${path}`);
    }
    const projectCode = body.projectCode;
    const db = query.private ? dbs.private : dbs.public;
    const result = await createOneProject(db, projectCode, body, headers);
    // Add Content-Location header on success so client knows where to find the newly-created project
    if (result && result.status && result.status >= 200 && result.status < 300) {
        return { ...result, headers: { ...result.headers, 'Content-Location': `${path}/${projectCode}` } };
    } else {
        return result;
    }
}
