import { Project, defaultRoleId } from '$lib/db/models';
import { dbs } from '$lib/db/dbsetup';
import { missingRequiredParam } from '$lib/utils/commonErrors';
import { onlyOne } from '$lib/utils/commonSqlHandlers';
import { addUserWithRoleByProjectCode, removeUserFromProjectByProjectCode } from '$lib/utils/db/usersAndRoles';

// GET /api/projects/{projectCode}/user/{username} - return user's role
export async function get({ params, query }) {
    if (!params || !params.projectCode) {
        return missingRequiredParam('projectCode', 'URL');
    }
    if (!params || !params.username) {
        return missingRequiredParam('username', 'URL');
    }
    const db = query.private ? dbs.private : dbs.public;
    const dbQuery = Project.query(db)
        .where('identifier', params.projectCode)
        .withGraphJoined('members.[user, role]')
        ;
    return onlyOne(dbQuery, 'projectCode', 'project code',
    async (project) => {
        const users = project.members.filter(member => member.user.login === params.username && member.role && member.role.name);
        return onlyOne(users, 'username', 'username', (member) => ({ status: 200, body: { user: member.user, role: member.role.name }}));
    });
}

// TODO: HEAD /api/projects/{projectCode}/user/{username} - is user a member of the project?
// Possible return codes:
// 200 if user is member of project (with any role)
// 404 if he/she is not, or if user or project not found
// export async function head({ path }) {
//     return { status: 204, body: {} }
// }

// DELETE /api/projects/{projectCode}/user/{username} - remove user from project
export async function del({ params, query }) {
    if (!params || !params.projectCode) {
        return missingRequiredParam('projectCode', 'URL');
    }
    if (!params || !params.username) {
        return missingRequiredParam('username', 'URL');
    }
    const db = query.private ? dbs.private : dbs.public;
    return removeUserFromProjectByProjectCode(db, params.projectCode, params.username);
}

export async function post({ params, path, body, query }) {
    if (!params.projectCode) {
        return missingRequiredParam('projectCode', path);
    }
    if (!params.username) {
        return missingRequiredParam('username', path);
    }
    let roleName;
    if (body && typeof body === "string") {
        roleName = body;
    } else if (body && typeof body === "number") {
        roleName = body;
    } else if (body && typeof body === "object") {
        roleName = body.role ? body.role : body.roleId ? body.roleId : body.roleName ? body.roleName : defaultRoleId;
    } else {
        roleName = defaultRoleId;
    }
    const db = query.private ? dbs.private : dbs.public;
    return addUserWithRoleByProjectCode(db, params.projectCode, params.username, roleName);
}
