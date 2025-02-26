import { User } from '$lib/db/models';
import { dbs } from '$lib/db/dbsetup';
import { missingRequiredParam } from '$lib/utils/commonErrors';
import { catchSqlError } from '$lib/utils/commonSqlHandlers';
import { verifyJwtAuth } from '$lib/utils/db/auth';
import { isAdmin } from '$lib/utils/db/authRules';

export async function get({ params, query, path, headers }) {
    const db = query.private ? dbs.private : dbs.public;
    if (!params.searchTerm) {
        return missingRequiredParam('searchTerm', path);
    }
    return catchSqlError(async () => {
        let search = User.query(db)
            .leftJoinRelated('emails')
            .where('login', 'like', `%${params.searchTerm}%`)
            .orWhere('firstname', 'like', `%${params.searchTerm}%`)
            .orWhere('lastname', 'like', `%${params.searchTerm}%`)
            .orWhere('address', 'like', `%${params.searchTerm}%`)
            ;

        // Anyone can search, but only admins get to see email addresses in the result
        const authUser = await verifyJwtAuth(db, headers);
        if (isAdmin(authUser)) {
            search = search.select('users.*', 'emails.address');
        } else {
            search = search.select('users.*');
        }

        const limit = query.get('limit');
        if (limit) {
            search = search.limit(limit);
        }
        const offset = query.get('offset');
        if (offset) {
            search = search.offset(offset);
        }

        const users = await search;
        return { status: 200, body: users };
    });
}

// NOTE: To check for duplicate email addresses:
// select * from email_addresses as e1 where exists (select 1 from email_addresses as e2 where e1.user_id = e2.user_id and e1.id <> e2.id);
